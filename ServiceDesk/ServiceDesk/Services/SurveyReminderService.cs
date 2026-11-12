using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Services;

// Çözülen ama değerlendirilmemiş talepler için anket hatırlatma e-postası.
//
// Çözümün hemen ardından gönderilen tek davet çoğu zaman gözden kaçar;
// memnuniyet verisi de bu yüzden az sayıda yanıta dayanır (ISO 9001 md. 9.1.2
// müşteri algısının izlenmesini ister). Bu iş, çözümden
// Survey:ReminderAfterHours saat sonra hâlâ doldurulmamış anketleri bir kez
// hatırlatır.
//
// Talep başına YALNIZCA BİR hatırlatma gider: gönderim anı
// Ticket.SurveyReminderSentAt'e yazılır, sorgu da bu alanın boş olmasını
// şart koşar. Uygulama yeniden başlasa, iş iki kez tetiklense de kullanıcı
// ikinci e-postayı almaz. Talep yeniden açılıp tekrar çözülürse alan
// temizlenir (TicketWorkflow.Apply) ve yeni pencere için yeniden hatırlatılır.
//
// Gönderimi bu iş YAPMAZ: NotificationService.QueueEmail ile kuyruğa yazar ve
// SignalDelivery ile MessageQueueWorker'ı uyandırır — yeniden deneme,
// sağlayıcı hatası ve şablon işleme zaten orada (NotificationDispatcher).
//
// Desen SlaMonitorService ile aynı: BackgroundService + PeriodicTimer +
// IServiceScopeFactory.
public class SurveyReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly SurveySettings _cfg;
    private readonly ILogger<SurveyReminderService> _log;

    public SurveyReminderService(IServiceScopeFactory scopes, IOptions<SurveySettings> cfg,
                                 ILogger<SurveyReminderService> log)
    {
        _scopes = scopes;
        _cfg = cfg.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cfg.ReminderEnabled)
        {
            _log.LogInformation("Anket hatırlatma yapılandırmada kapalı (Survey:ReminderEnabled)");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _cfg.CheckMinutes)));

        // Uygulama açılır açılmaz bir tur at, sonra ayardaki aralıkla
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Tarama sürerken talep güncellenmiş; kullanıcının değişikliği
                // korunur, talep bir sonraki turda yeniden değerlendirilir
                _log.LogInformation("Anket hatırlatma turu eşzamanlı bir talep güncellemesine denk geldi; sonraki turda tekrar denenecek");
            }
            catch (Exception ex)
            {
                // Bir tur hata alırsa servis ölmemeli; sonraki turda tekrar dener
                _log.LogError(ex, "Anket hatırlatma turu başarısız oldu");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    // Bir turu çalıştırır, kuyruğa alınan hatırlatma sayısını döner.
    public async Task<int> RunOnceAsync(CancellationToken token = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notify = scope.ServiceProvider.GetRequiredService<NotificationService>();

        // Doldurulacak anket yoksa hatırlatma da gönderilmez: kullanıcıyı
        // formun hiç görünmediği bir sayfaya yollamak güven kaybettirir.
        // (Aktif şablon kuralı: SurveyTemplateRules / SurveysController)
        var anketVar = await db.SurveyQuestions
            .AnyAsync(q => q.IsActive && q.Template!.IsActive, token);
        if (!anketVar) return 0;

        var now = AppTime.UtcNow;
        var hatirlatmaEsigi = now.AddHours(-Math.Max(0, _cfg.ReminderAfterHours));

        // Süre sınırı kapalıysa (0) üst sınır aranmaz
        var sonGecerlilik = _cfg.ExpiresAfterDays > 0
            ? now.AddDays(-_cfg.ExpiresAfterDays)
            : (DateTime?)null;

        // Filtreleme SQL tarafında: çözülmüş, anketi doldurulmamış, daha önce
        // hatırlatılmamış, gecikmesi dolmuş ve süresi geçmemiş talepler.
        // SurveyInvitedAt bu geçişten önceki kayıtlarda boş kalabilir
        // (migration geriye dönük doldurur); yine de ResolvedAt'e düşülür.
        var tickets = await db.Tickets
            .Include(t => t.Requester)
            .Where(t => t.ResolvedAt != null
                        && t.SurveyReminderSentAt == null
                        && t.Survey == null
                        && (t.SurveyInvitedAt ?? t.ResolvedAt) <= hatirlatmaEsigi
                        && (sonGecerlilik == null || (t.SurveyInvitedAt ?? t.ResolvedAt) > sonGecerlilik)
                        // Alıcı: e-postası olan aktif hesap. Pasif hesaba
                        // hatırlatma gönderilmez ama alan da işaretlenmez;
                        // hesap geri açılırsa hatırlatma yine gidebilir.
                        && t.Requester!.IsActive
                        && t.Requester!.Email != "")
            .OrderBy(t => t.SurveyInvitedAt ?? t.ResolvedAt)
            .Take(Math.Clamp(_cfg.BatchSize, 1, 1000))
            .ToListAsync(token);

        if (tickets.Count == 0) return 0;

        var kuyrukta = 0;

        foreach (var t in tickets)
        {
            // Saf kural son kez burada da uygulanır: sorgu ile politika
            // (SurveyPolicy) ayrışırsa e-posta değil kural kazanır
            if (!SurveyPolicy.ReminderDue(t, now, _cfg.ReminderAfterHours, _cfg.ExpiresAfterDays))
                continue;

            var kalan = SurveyPolicy.Deadline(t, _cfg.ExpiresAfterDays) is { } deadline
                ? $" Anketi doldurmak için {SurveyPolicy.RemainingText(deadline, now)} süreniz var "
                  + $"(son tarih {AppTime.ToLocal(deadline):dd.MM.yyyy HH:mm})."
                : "";

            notify.QueueEmail(t.RequesterId, NotificationType.SurveyReminder,
                $"Değerlendirmenizi bekliyoruz: {Ui.TicketNo(t)} {t.Title}",
                $"\"{t.Title}\" talebiniz çözüldü ama henüz değerlendirmediniz."
                + " Birkaç soruluk anket, hizmeti nerede iyileştirmemiz gerektiğini gösteriyor."
                + kalan,
                t.Id, EmailTemplates.SurveyReminder);

            // İkinci hatırlatmayı engelleyen işaret. Kuyruk kaydıyla aynı
            // SaveChanges'te yazılır: ya ikisi birlikte olur ya hiçbiri.
            t.SurveyReminderSentAt = now;
            kuyrukta++;
        }

        if (kuyrukta == 0) return 0;

        await db.SaveChangesAsync(token);

        // Worker'ı uyandır: gönderim, yeniden deneme ve şablon işleme orada
        notify.SignalDelivery();

        _log.LogInformation("Anket hatırlatma: {Count} talep için e-posta kuyruğa alındı", kuyrukta);
        return kuyrukta;
    }
}
