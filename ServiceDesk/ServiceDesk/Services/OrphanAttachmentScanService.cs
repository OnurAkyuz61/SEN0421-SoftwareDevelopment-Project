using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Uploads/ klasöründe Attachments tablosunda karşılığı olmayan ("sahipsiz")
// dosyaları bulur.
//
// Böyle dosyalar birikebilir: yükleme sırasında isteğin yarıda kesilmesi
// (dosya diske ek kaydından ÖNCE yazılır — FileStorage.SaveAsync), klasöre
// elle kopyalanan dosyalar, veritabanının eski bir yedekten geri yüklenip
// diskin olduğu gibi kalması. Hiçbir ekranda görünmezler ama diski doldurur
// ve denetimde "bu dosya nedir, kimin?" sorusu cevapsız kalır.
//
// İLK SÜRÜM YALNIZCA RAPOR EDER. Silme ayrı bir ayarla açılır
// (Uploads:OrphanScan:DeleteFiles, varsayılan false): burada yanlış pozitif
// kullanıcının gerçek dosyasını silmek demek, o yüzden önce raporun loglardan
// doğrulanması gerekir.
//
// Desen SlaMonitorService / KpiAggregatorService ile aynı:
// BackgroundService + PeriodicTimer + IServiceScopeFactory.
public class OrphanAttachmentScanService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly OrphanScanSettings _cfg;
    private readonly ILogger<OrphanAttachmentScanService> _log;

    public OrphanAttachmentScanService(IServiceScopeFactory scopes, IOptions<UploadSettings> cfg,
                                       ILogger<OrphanAttachmentScanService> log)
    {
        _scopes = scopes;
        _cfg = cfg.Value.OrphanScan;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cfg.Enabled)
        {
            _log.LogInformation("Sahipsiz dosya taraması yapılandırmada kapalı (Uploads:OrphanScan:Enabled)");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _cfg.ScanMinutes)));
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
            catch (Exception ex)
            {
                // Bir tur hata alırsa servis ölmemeli; sonraki turda tekrar dener
                _log.LogError(ex, "Sahipsiz dosya taraması başarısız oldu");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    // Bir turun sonucu: bulunan sahipsiz dosya sayısı / toplam boyut ve
    // (silme açıksa) gerçekten silinen sayı.
    public sealed record ScanOutcome(int Found, long Bytes, int Deleted);

    // Bir dosya sahipsiz mi (saf; birim testi yazılabilsin diye ayrı).
    // knownNames: Attachments tablosundaki diskteki adlar — yumuşak silinmiş
    // ekler DAHİL, onların dosyası saklama süresi boyunca diskte durur.
    public static bool IsOrphan(string fileName, DateTime lastWriteUtc, DateTime nowUtc,
                                ISet<string> knownNames, int minAgeMinutes) =>
        !knownNames.Contains(fileName)
        && nowUtc - lastWriteUtc >= TimeSpan.FromMinutes(Math.Max(0, minAgeMinutes));

    public async Task<ScanOutcome> RunOnceAsync(CancellationToken token = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<FileStorage>();
        var audit = scope.ServiceProvider.GetRequiredService<AuditService>();

        var root = files.Root;
        if (!Directory.Exists(root)) return new ScanOutcome(0, 0, 0);

        // Klasör düz: FileStorage her dosyayı doğrudan Uploads/ içine yazar
        var onDisk = new DirectoryInfo(root).GetFiles();
        if (onDisk.Length == 0) return new ScanOutcome(0, 0, 0);

        // IgnoreQueryFilters: kaldırılmış ekin ve silinmiş talebin ekinin
        // dosyası da diskte durur, bunlar sahipsiz DEĞİL. Onları saklama
        // süresi dolunca AttachmentRetentionService temizler.
        var known = await db.Attachments
            .IgnoreQueryFilters()
            .Select(a => a.StoredFileName)
            .ToListAsync(token);

        // Dosya sistemi (Windows) büyük/küçük harfe duyarsız: karşılaştırma da öyle
        var knownNames = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);

        var now = AppTime.UtcNow;
        var orphans = onDisk
            .Where(f => IsOrphan(f.Name, f.LastWriteTimeUtc, now, knownNames, _cfg.MinAgeMinutes))
            .OrderBy(f => f.LastWriteTimeUtc)
            .ToList();

        if (orphans.Count == 0)
        {
            _log.LogInformation("Sahipsiz dosya taraması: {Total} dosyanın tamamının kaydı var", onDisk.Length);
            return new ScanOutcome(0, 0, 0);
        }

        var bytes = orphans.Sum(f => f.Length);
        Report(orphans, bytes, onDisk.Length);

        if (!_cfg.DeleteFiles)
        {
            // Rapor modu: dosyalara dokunulmaz, ne yapılabileceği söylenir
            _log.LogInformation(
                "Silme kapalı (Uploads:OrphanScan:DeleteFiles=false): dosyalar yerinde bırakıldı");
            return new ScanOutcome(orphans.Count, bytes, 0);
        }

        var deleted = 0;
        long freed = 0;

        foreach (var f in orphans)
        {
            if (token.IsCancellationRequested) break;
            if (!TryDelete(f)) continue;

            // Talebe bağlanamaz (kaydı yok): denetim kaydı ticketId'siz yazılır
            audit.Queue(AuditAction.AttachmentOrphanDeleted,
                detail: $"Kaydı olmayan dosya silindi: {f.Name} ({Ui.FileSize(f.Length)}), "
                      + $"son değişiklik {AppTime.ToLocal(f.LastWriteTimeUtc):dd.MM.yyyy HH:mm}");

            deleted++;
            freed += f.Length;
        }

        if (deleted > 0)
        {
            await db.SaveChangesAsync(token);
            _log.LogInformation("Sahipsiz {Count} dosya silindi, {Size} yer boşaldı", deleted, Ui.FileSize(freed));
        }

        return new ScanOutcome(orphans.Count, bytes, deleted);
    }

    // Bulguyu tek satırda yazar: ad listesi MaxReported ile sınırlı ki
    // binlerce dosyada log dosyası şişmesin.
    private void Report(List<FileInfo> orphans, long bytes, int total)
    {
        var max = Math.Clamp(_cfg.MaxReported, 1, 100);
        var names = string.Join(", ", orphans.Take(max).Select(f => f.Name));
        var more = orphans.Count > max ? $" (+{orphans.Count - max} dosya daha)" : "";

        _log.LogWarning(
            "Sahipsiz dosya taraması: Uploads klasöründeki {Total} dosyanın {Count} tanesinin "
            + "Attachments tablosunda karşılığı yok, toplam {Size}: {Names}{More}",
            total, orphans.Count, Ui.FileSize(bytes), names, more);
    }

    private bool TryDelete(FileInfo f)
    {
        try
        {
            f.Delete();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Kilitli / izin yok: job düşmez, dosya sonraki turda yine raporlanır
            _log.LogWarning(ex, "Sahipsiz dosya silinemedi: {Name}", f.Name);
            return false;
        }
    }
}
