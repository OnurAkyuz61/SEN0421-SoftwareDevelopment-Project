using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Saklama süresi dolmuş ek dosyaları diskten ve tablodan kalıcı olarak siler.
//
// Ek "kaldırıldığında" kayıt silinmiyor, işaretleniyor (Attachment.IsDeleted)
// ve dosya Uploads/ içinde duruyor: yanlışlıkla silme geri alınabilsin,
// denetimde "ne kaldırılmış" sorusu cevaplanabilsin. Ama bu bekleme sonsuz
// olamaz — ISO 9001 md. 7.5.3 kayıtların elden çıkarılmasının da tanımlı
// olmasını ister, KVKK tarafında da gereğinden uzun saklama bir risktir.
// Süre (Uploads:Retention:Days) dolan ek burada temizlenir ve temizlik
// AuditLogs'a yazılır: dosyanın kaybolmadığı, kuralla silindiği görünür.
//
// Desen SlaMonitorService / KpiAggregatorService ile aynı:
// BackgroundService + PeriodicTimer + IServiceScopeFactory.
public class AttachmentRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly RetentionSettings _cfg;
    private readonly ILogger<AttachmentRetentionService> _log;

    public AttachmentRetentionService(IServiceScopeFactory scopes, IOptions<UploadSettings> cfg,
                                      ILogger<AttachmentRetentionService> log)
    {
        _scopes = scopes;
        _cfg = cfg.Value.Retention;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cfg.Enabled)
        {
            _log.LogInformation("Ek dosya saklama temizliği yapılandırmada kapalı (Uploads:Retention:Enabled)");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _cfg.SweepMinutes)));

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
                // Temizlik sürerken aynı ek üzerinde bir işlem olmuş; kayıt
                // olduğu gibi kalır, bir sonraki turda yeniden değerlendirilir
                _log.LogInformation("Ek dosya temizliği eşzamanlı bir güncellemeye denk geldi; sonraki turda tekrar denenecek");
            }
            catch (Exception ex)
            {
                // Bir tur hata alırsa servis ölmemeli; sonraki turda tekrar dener
                _log.LogError(ex, "Ek dosya saklama temizliği başarısız oldu");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    // Saklama süresi dolmuş mu (saf; birim testi yazılabilsin diye ayrı)
    public static bool IsDue(DateTime? deletedAtUtc, DateTime nowUtc, int retentionDays) =>
        deletedAtUtc != null && nowUtc - deletedAtUtc.Value >= TimeSpan.FromDays(Math.Max(1, retentionDays));

    // Bir turu çalıştırır, kalıcı olarak silinen ek sayısını döner.
    public async Task<int> RunOnceAsync(CancellationToken token = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<FileStorage>();
        // AuditService aynı kapsamdaki AppDbContext'e yazar: denetim kaydı ile
        // silme aynı SaveChanges'te, yani ya ikisi birlikte olur ya hiçbiri
        var audit = scope.ServiceProvider.GetRequiredService<AuditService>();

        var days = Math.Max(1, _cfg.Days);
        var cutoff = AppTime.UtcNow.AddDays(-days);

        // IgnoreQueryFilters: kaldırılmış ek global filtreye zaten düşmez,
        // bu job'ın baktığı kayıtlar tam olarak onlar. Koşul elle yazılıyor —
        // yalnızca yumuşak silinmiş ve süresi geçmiş ekler temizlenir; silinmiş
        // bir talebin (henüz) kaldırılmamış ekine dokunulmaz.
        var due = await db.Attachments
            .IgnoreQueryFilters()
            .Where(a => a.IsDeleted && a.DeletedAt != null && a.DeletedAt < cutoff)
            .OrderBy(a => a.DeletedAt)
            .Take(Math.Clamp(_cfg.BatchSize, 1, 1000))
            .ToListAsync(token);

        if (due.Count == 0) return 0;

        var purged = 0;
        long freed = 0;

        foreach (var a in due)
        {
            if (token.IsCancellationRequested) break;

            // Önce disk, sonra kayıt: dosya silinemediyse kayıt duruyor ve iş
            // bir sonraki turda yeniden denenir. Tersi sırada kayıt gidip dosya
            // kalsa izi kaybolurdu (sahipsiz dosya taramasına kalırdı).
            if (!TryDeleteFile(files, a)) continue;

            audit.Queue(AuditAction.AttachmentPurged,
                userId: a.DeletedById,
                detail: $"\"{a.FileName}\" ({Ui.FileSize(a.Size)}) diskten ve kayıttan kalıcı olarak silindi; "
                      + $"{days} günlük saklama süresi {AppTime.ToLocal(a.DeletedAt!.Value):dd.MM.yyyy HH:mm} tarihinde dolmuştu",
                ticketId: a.TicketId);

            db.Attachments.Remove(a);
            purged++;
            freed += a.Size;
        }

        if (purged == 0) return 0;

        await db.SaveChangesAsync(token);

        _log.LogInformation(
            "Saklama süresi ({Days} gün) dolan {Count} ek dosya kalıcı olarak silindi, {Size} yer boşaldı",
            days, purged, Ui.FileSize(freed));

        // Batch dolduysa birikmiş kayıt var; bir sonraki tur devam eder
        if (due.Count >= Math.Clamp(_cfg.BatchSize, 1, 1000))
            _log.LogInformation("Temizlik sırası dolu geldi; kalanlar sonraki turda silinecek");

        return purged;
    }

    // Diskteki dosyayı siler. Dosya yoksa (elle silinmiş, iki kez işlenmiş)
    // bu da başarı sayılır: kayıt da gitmeli. false dönerse kayıt korunur.
    private bool TryDeleteFile(FileStorage files, Attachment a)
    {
        if (!FileStorage.IsSafeStoredName(a.StoredFileName))
        {
            // Beklenmeyen ad: Uploads dışına çıkan bir yolu silmek yerine
            // kayda dokunmuyoruz, yönetici görsün diye uyarı bırakıyoruz
            _log.LogWarning("Ek #{Id} beklenmeyen bir dosya adı taşıyor ({Name}); temizlik atlandı",
                a.Id, a.StoredFileName);
            return false;
        }

        try
        {
            File.Delete(files.PathOf(a));   // olmayan dosyada sessizce geçer
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Dosya kilitli ya da izin yok: job düşmez, kayıt da silinmez
            _log.LogWarning(ex, "Ek #{Id} dosyası silinemedi ({Name}); sonraki turda yeniden denenecek",
                a.Id, a.StoredFileName);
            return false;
        }
    }
}
