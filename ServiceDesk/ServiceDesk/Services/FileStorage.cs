using Microsoft.Extensions.Options;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Yüklenen dosyaları wwwroot DIŞINDA (Uploads/) saklar; böylece link bilen herkes indiremez.
// Uzantı, içerik türü ve dosya imzası kuralları UploadPolicy'de.
//
// Sınırlar artık const değil, appsettings.json → "Uploads" bölümünden geliyor
// (UploadSettings). Bu yüzden Validate de statik değil, örnek (instance)
// metodu: çağıran zaten FileStorage'ı enjekte ediyor.
//
// Diske yazılan her dosya yazıldığı anda antivirüs taramasından geçer
// (IAttachmentScanner); sonuç ekin kaydına işlenir. Şüpheli dosya silinmez —
// karantinada kalır ki denetimde "ne yüklenmeye çalışıldı" sorusu
// cevaplanabilsin — ama AttachmentsController.Get onu servis etmez.
public class FileStorage
{
    public static IReadOnlyCollection<string> AllowedExt => UploadPolicy.AllowedExtensions;

    private readonly string _root;
    private readonly UploadSettings _cfg;
    private readonly IAttachmentScanner _scanner;

    public FileStorage(IWebHostEnvironment env, IOptions<UploadSettings> cfg, IAttachmentScanner scanner)
    {
        _root = Path.Combine(env.ContentRootPath, "Uploads");
        Directory.CreateDirectory(_root);
        _cfg = cfg.Value;
        _scanner = scanner;
    }

    // Görünümlerin (ör. _UploadField) sınırları kullanıcıya yazabilmesi için
    public UploadSettings Settings => _cfg;

    // Hata varsa mesajı döner, yoksa null.
    // kind: boyut sınırı talep ekiyle yorum eki için ayrı tanımlıdır.
    public string? Validate(IList<IFormFile>? files, UploadKind kind)
    {
        if (files == null || files.Count == 0) return null;
        if (files.Count > _cfg.MaxFiles) return $"En fazla {_cfg.MaxFiles} dosya yükleyebilirsiniz.";

        var max = _cfg.MaxBytesFor(kind);
        foreach (var f in files)
        {
            var name = UploadPolicy.SafeFileName(f.FileName);
            if (f.Length == 0) return $"\"{name}\" boş bir dosya.";
            if (f.Length > max)
                return $"\"{name}\" dosya başına {Ui.FileSize(max)} sınırını aşıyor ({Ui.FileSize(f.Length)}).";

            // Üç katlı içerik doğrulaması (uzantı + bildirilen tür + imza)
            using var stream = f.OpenReadStream();
            if (UploadPolicy.Check(f.FileName, f.ContentType, stream) is { } error) return error;
        }
        return null;
    }

    // Diske yazılmış ve taranmış dosya. Hangi TABLOYA yazılacağından bağımsız:
    // talep eki (Attachment) ve uygunsuzluk kanıtı (NonconformityAttachment)
    // aynı doğrulama + tarama yolundan geçer, yalnızca kaydı farklı yerde durur.
    public sealed record StoredFile(
        string FileName, string StoredFileName, string ContentType, long Size,
        AttachmentScanStatus ScanStatus, DateTime ScannedAt);

    public async Task<List<Attachment>> SaveAsync(IList<IFormFile>? files, CancellationToken token = default)
    {
        var list = new List<Attachment>();
        foreach (var f in await StoreAsync(files, token))
            list.Add(new Attachment
            {
                FileName = f.FileName,
                StoredFileName = f.StoredFileName,
                ContentType = f.ContentType,
                Size = f.Size,
                ScanStatus = f.ScanStatus,
                ScannedAt = f.ScannedAt
            });
        return list;
    }

    // Uygunsuzluk kanıtı için (NonconformityAttachment); kayıt alanları
    // çağıranda doldurulur.
    public async Task<List<NonconformityAttachment>> SaveEvidenceAsync(
        IList<IFormFile>? files, CancellationToken token = default)
    {
        var list = new List<NonconformityAttachment>();
        foreach (var f in await StoreAsync(files, token))
            list.Add(new NonconformityAttachment
            {
                FileName = f.FileName,
                StoredFileName = f.StoredFileName,
                ContentType = f.ContentType,
                Size = f.Size,
                ScanStatus = f.ScanStatus,
                ScannedAt = f.ScannedAt
            });
        return list;
    }

    public async Task<List<StoredFile>> StoreAsync(IList<IFormFile>? files, CancellationToken token = default)
    {
        var list = new List<StoredFile>();
        if (files == null) return list;

        foreach (var f in files.Where(f => f.Length > 0))
        {
            var name = UploadPolicy.SafeFileName(f.FileName);
            var ext = Path.GetExtension(name).ToLowerInvariant();

            // Çağıran Validate'i atlamış olsa bile izin listesi dışı dosya diske yazılmaz
            if (!UploadPolicy.IsAllowedExtension(ext)) continue;

            var stored = Guid.NewGuid().ToString("N") + ext;
            var path = Path.Combine(_root, stored);
            await using (var stream = File.Create(path))
                await f.CopyToAsync(stream, token);

            // Tarama dosya kapandıktan sonra: sağlayıcı dosyayı yoldan okur
            var scan = await _scanner.ScanAsync(path, name, token);

            list.Add(new StoredFile(
                FileName: name,
                StoredFileName: stored,
                // İçerik türü istemcinin beyanından değil, doğrulanmış uzantıdan gelir
                ContentType: UploadPolicy.ContentTypeFor(stored),
                Size: f.Length,
                ScanStatus: scan.Status,
                ScannedAt: AppTime.UtcNow));
        }
        return list;
    }

    public string PathOf(Attachment a) => PathOf(a.StoredFileName);

    // Bakım job'ları (AttachmentRetentionService / OrphanAttachmentScanService)
    // klasörün kendisini gezmek zorunda: kökü tek yerden alsınlar
    public string Root => _root;

    public string PathOf(string storedFileName) => Path.Combine(_root, storedFileName);

    // Diskteki ad guid + uzantıdır. Alt klasör ya da ".." içeren bir ad
    // beklenmez; silme yapan taraf buradan geçmeden dosyaya dokunmaz ki
    // bozuk bir kayıt Uploads dışında bir dosyayı silmeye yol açmasın.
    public static bool IsSafeStoredName(string? storedFileName) =>
        !string.IsNullOrWhiteSpace(storedFileName)
        && Path.GetFileName(storedFileName) == storedFileName;
}
