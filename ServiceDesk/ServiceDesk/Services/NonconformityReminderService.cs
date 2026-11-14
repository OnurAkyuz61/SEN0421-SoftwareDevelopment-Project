using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Services;

// Hatırlatmanın aşaması. "Yaklaşıyor" ve "geçti" ayrı mesajlardır: biri
// işi öne almaya çağırır, öteki gecikmeyi bildirir.
public enum NonconformityReminderKind
{
    None = 0,
    DueSoon = 1,    // termine ReminderBeforeHours'tan az kaldı
    Overdue = 2     // termin geçti (Nonconformity.IsOverdue)
}

// Uygunsuzluk termini yaklaşan / geçen kayıtlar için hatırlatma job'ı.
//
// ISO 9001 md. 10.2 düzeltici faaliyetin yalnızca başlatılmasını değil TAKİP
// edilmesini de ister. Termin alanı baştan beri vardı ama hiçbir yerde
// izlenmiyordu: kayıt sessizce gecikiyor, denetimde "açıldı ama kimse
// bakmadı" olarak çıkıyordu.
//
// Kayıt başına AYNI AŞAMADA ikinci hatırlatma gitmez: gönderim anı
// Nonconformity.ReminderSentAt'e yazılır ve aşama bu anın TERMİNE GÖRE
// konumundan okunur:
//   • ReminderSentAt yok                     → iki aşama da gönderilebilir
//   • ReminderSentAt termin tarihinden ÖNCE  → "yaklaşıyor" gönderilmiş;
//                                              termin geçtiğinde bir kez daha
//                                              (bu kez "geçti") gider
//   • ReminderSentAt termin tarihinden SONRA → "geçti" de gönderilmiş, biter
// Böylece tek alanla iki aşamanın her biri BİR kez hatırlatılır; termin
// ileri alınırsa pencere yeniden açılır (yeni termin yeni bir takip süresidir).
//
// Alıcı kaydın sorumlusudur. Sorumlusu olmayan kayıt (atanabilecek kimse
// bulunamamış ya da sorumlu temizlenmiş) yöneticilere bildirilir: sahipsiz
// kalite kaydı hiç işlenmez — md. 5.1, kalite sorumluluğu yönetimdedir.
//
// Gönderimi bu iş YAPMAZ: NotificationService.Queue ile kuyruğa yazar,
// SignalDelivery ile MessageQueueWorker'ı uyandırır.
//
// Desen ApprovalReminderService ile aynı: BackgroundService + PeriodicTimer +
// IServiceScopeFactory; karar veren kural saf statik metotta (birim testi).
public class NonconformityReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly QualitySettings _cfg;
    private readonly ILogger<NonconformityReminderService> _log;

    public NonconformityReminderService(IServiceScopeFactory scopes, IOptions<QualitySettings> cfg,
                                        ILogger<NonconformityReminderService> log)
    {
        _scopes = scopes;
        _cfg = cfg.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cfg.ReminderEnabled)
        {
            _log.LogInformation("Uygunsuzluk termin hatırlatması yapılandırmada kapalı (Quality:ReminderEnabled)");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _cfg.ReminderCheckMinutes)));
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (DbUpdateConcurrencyException)
            {
                // Tarama sürerken kayıt güncellenmiş (ör. sorumlu kapattı);
                // kullanıcının değişikliği korunur, kayıt sonraki turda bakılır
                _log.LogInformation("Uygunsuzluk hatırlatma turu eşzamanlı güncellemeye denk geldi; sonraki turda tekrar denenecek");
            }
            catch (Exception ex) { _log.LogError(ex, "Uygunsuzluk hatırlatma turu başarısız"); }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    // Hangi hatırlatma gönderilecek (saf; test edilebilir).
    //
    // Kapalı kaydın ve termini olmayan kaydın takibi yoktur. Aşama başına tek
    // gönderim kuralı ReminderSentAt'in termine göre konumundan okunur.
    public static NonconformityReminderKind KindFor(
        NonconformityStatus status, DateTime? dueAtUtc, DateTime? reminderSentAtUtc,
        DateTime nowUtc, int beforeHours)
    {
        if (status == NonconformityStatus.Closed || dueAtUtc is not { } due)
            return NonconformityReminderKind.None;

        if (nowUtc > due)
            // Termin geçti: daha önce hiç hatırlatılmadıysa ya da yalnızca
            // "yaklaşıyor" hatırlatması gittiyse gecikme bildirilir
            return reminderSentAtUtc == null || reminderSentAtUtc.Value <= due
                ? NonconformityReminderKind.Overdue
                : NonconformityReminderKind.None;

        // Termin gelecekte: pencereye girdiyse ve bu aşamada hiç
        // hatırlatılmadıysa "yaklaşıyor" gider
        var window = TimeSpan.FromHours(Math.Max(0, beforeHours));
        return reminderSentAtUtc == null && due - nowUtc <= window
            ? NonconformityReminderKind.DueSoon
            : NonconformityReminderKind.None;
    }

    // Bildirim metni (saf; test edilebilir). Kalan / geçen süre insan okur
    // gibi yazılır: "36 saat", "3 gün".
    public static (string Title, string Message, string TemplateKey) Compose(
        Nonconformity n, NonconformityReminderKind kind, DateTime nowUtc)
    {
        var due = n.DueAt!.Value;
        var termin = $"Termin: {AppTime.ToLocal(due):dd.MM.yyyy HH:mm}.";
        const string eksik = "Kapanış için kök neden, düzeltici faaliyet, "
                             + "etkinlik doğrulaması ve kanıt dosyası gerekiyor.";

        return kind == NonconformityReminderKind.Overdue
            ? ($"Uygunsuzluk #{n.Id} termini geçti",
               $"{termin} {Elapsed(due, nowUtc)} gecikmede. {eksik}",
               EmailTemplates.NonconformityOverdue)
            : ($"Uygunsuzluk #{n.Id} termini yaklaşıyor",
               $"{termin} Kalan süre {Elapsed(nowUtc, due)}. {eksik}",
               EmailTemplates.NonconformityReminder);
    }

    // İki an arasındaki süre: 1 saatin altı dakika, 1 günün altı saat, üstü gün
    public static string Elapsed(DateTime fromUtc, DateTime toUtc)
    {
        var span = toUtc - fromUtc;
        if (span <= TimeSpan.Zero) return "0 dakika";
        if (span.TotalHours < 1) return $"{Math.Max(1, (int)span.TotalMinutes)} dakika";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} saat";
        return $"{(int)span.TotalDays} gün";
    }

    // Bir turu çalıştırır, kuyruğa alınan hatırlatma sayısını döner.
    public async Task<int> RunOnceAsync(CancellationToken token = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notify = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var now = AppTime.UtcNow;
        var ufuk = now.AddHours(Math.Max(0, _cfg.ReminderBeforeHours));

        // Kaba süzgeç SQL'de: kapanmamış, termini tanımlı ve termini
        // pencereye girmiş kayıtlar. Aşama başına tek gönderim kuralı
        // KindFor'da — ReminderSentAt karşılaştırması SQL'e de yazılsaydı
        // iki taraf zamanla ayrı düşerdi.
        var adaylar = await db.Nonconformities
            .Include(n => n.Ticket)
            .Where(n => n.Status != NonconformityStatus.Closed
                        && n.DueAt != null
                        && n.DueAt <= ufuk
                        && (n.ReminderSentAt == null || n.ReminderSentAt <= n.DueAt))
            .OrderBy(n => n.DueAt)
            .Take(Math.Clamp(_cfg.ReminderBatchSize, 1, 1000))
            .ToListAsync(token);

        if (adaylar.Count == 0) return 0;

        // Sorumlusu olmayan kayıtlar yöneticilere düşer; liste bir kez okunur
        List<int>? yoneticiler = null;

        var kuyrukta = 0;
        foreach (var n in adaylar)
        {
            var kind = KindFor(n.Status, n.DueAt, n.ReminderSentAt, now, _cfg.ReminderBeforeHours);
            if (kind == NonconformityReminderKind.None) continue;

            var (title, message, template) = Compose(n, kind, now);

            var alicilar = new List<int>();
            if (n.ResponsibleId is { } sorumlu)
                alicilar.Add(sorumlu);
            else
            {
                yoneticiler ??= await db.Users
                    .Where(u => u.IsActive && u.Role == UserRole.Admin)
                    .Select(u => u.Id).ToListAsync(token);
                alicilar.AddRange(yoneticiler);
            }

            // Alıcısı olmayan kayıt işaretlenmez: yönetici hesabı açıldığında
            // hatırlatma yine gidebilsin
            if (alicilar.Count == 0) continue;

            foreach (var id in alicilar)
                notify.Queue(id, NotificationType.NonconformityDueReminder,
                    title, message, n.TicketId, template);

            // İkinci hatırlatmayı engelleyen işaret. Kuyruk kaydıyla aynı
            // SaveChanges'te yazılır: ya ikisi birlikte olur ya hiçbiri.
            n.ReminderSentAt = now;
            kuyrukta++;
        }

        if (kuyrukta == 0) return 0;

        await db.SaveChangesAsync(token);
        notify.SignalDelivery();

        _log.LogInformation("Uygunsuzluk termin hatırlatması: {Count} kayıt için kuyruğa alındı", kuyrukta);
        return kuyrukta;
    }
}
