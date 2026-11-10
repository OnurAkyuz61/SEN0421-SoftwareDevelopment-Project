using Microsoft.Extensions.Options;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Tarama sonucu. İstisna fırlatmak yerine sonuç döner ki çağıran (FileStorage)
// taramayı kayda yazabilsin: taranamayan dosya da "güvenli değil" sayılır.
public sealed record ScanResult(
    AttachmentScanStatus Status,
    string Provider,
    string? Detail = null)
{
    public static ScanResult Skipped(string provider) =>
        new(AttachmentScanStatus.Skipped, provider, "Tarama yapılandırmada kapalı");
}

// Yüklenen dosyanın zararlı içerik taraması. Gerçek sağlayıcı (ClamAV,
// Defender, bulut tarama) bağlanınca yalnızca Program.cs'teki kayıt değişir;
// bu arayüz sabit kalır — ISmsSender / IWhatsAppSender ile aynı desen.
public interface IAttachmentScanner
{
    bool IsEnabled { get; }

    // path: Uploads/ içindeki dosyanın tam yolu, fileName: kullanıcıya görünen ad
    Task<ScanResult> ScanAsync(string path, string fileName, CancellationToken token = default);
}

// Gerçek antivirüs bağlanana kadar taramayı loglar.
//
// Tamamen "her şey temiz" demiyor: kapıyı uçtan uca sınayabilmek için
// standart antivirüs TEST imzasını (EICAR) arar. Böylece test dosyası
// yüklendiğinde durum Suspicious olur ve AttachmentsController.Get dosyayı
// servis etmez — gate'in çalıştığı gerçek bir virüs olmadan doğrulanabilir.
//
// İmza kaynak dosyaya düz metin yazılmaz, base64 olarak tutulup çalışma
// anında çözülür: aksi halde geliştiricinin antivirüsü .cs dosyasını ya da
// derlenmiş DLL'i karantinaya alırdı.
public class LoggingAttachmentScanner : IAttachmentScanner
{
    private const string TestSignatureBase64 =
        "WDVPIVAlQEFQWzRcUFpYNTQoUF4pN0NDKTd9JEVJQ0FSLVNUQU5EQVJELUFOVElWSVJVUy1URVNULUZJTEUhJEgrSCo=";

    private static readonly byte[] TestSignature = Convert.FromBase64String(TestSignatureBase64);

    // İmza dosyanın başındadır; tüm dosyayı belleğe almaya gerek yok
    private const int HeadBytes = 8 * 1024;

    private readonly ILogger<LoggingAttachmentScanner> _log;
    private readonly ScannerSettings _cfg;

    public LoggingAttachmentScanner(ILogger<LoggingAttachmentScanner> log, IOptions<UploadSettings> cfg)
    {
        _log = log;
        _cfg = cfg.Value.Scanner;
    }

    public bool IsEnabled => _cfg.Enabled;

    public async Task<ScanResult> ScanAsync(string path, string fileName, CancellationToken token = default)
    {
        const string provider = "Logging-Scanner";

        if (!_cfg.Enabled)
        {
            _log.LogInformation("Ek tarama (stub) kapalı, atlandı: {File}", fileName);
            return ScanResult.Skipped(provider);
        }

        try
        {
            await using var fs = File.OpenRead(path);
            var buf = new byte[HeadBytes];
            var read = await fs.ReadAtLeastAsync(buf, buf.Length, throwOnEndOfStream: false, token);

            if (buf.AsSpan(0, read).IndexOf(TestSignature) >= 0)
            {
                _log.LogWarning("Ek tarama (stub): {File} test imzası taşıyor, ŞÜPHELİ işaretlendi.", fileName);
                return new ScanResult(AttachmentScanStatus.Suspicious, provider, "Antivirüs test imzası (EICAR)");
            }

            _log.LogInformation("Ek tarama (stub): {File} temiz ({Bytes} bayt okundu).", fileName, read);
            return new ScanResult(AttachmentScanStatus.Clean, provider);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Taranamayan dosya temiz sayılmaz: Get ucu Failed durumunu da servis etmez
            _log.LogError(ex, "Ek tarama (stub) başarısız: {File}", fileName);
            return new ScanResult(AttachmentScanStatus.Failed, provider, ex.Message);
        }
    }
}
