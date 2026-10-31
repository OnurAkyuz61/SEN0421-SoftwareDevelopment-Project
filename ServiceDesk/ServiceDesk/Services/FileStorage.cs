using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Yüklenen dosyaları wwwroot DIŞINDA (Uploads/) saklar; böylece link bilen herkes indiremez.
// Uzantı, içerik türü ve dosya imzası kuralları UploadPolicy'de.
public class FileStorage
{
    public const int MaxFiles = 5;
    public const long MaxBytes = 10 * 1024 * 1024; // 10 MB
    public static IReadOnlyCollection<string> AllowedExt => UploadPolicy.AllowedExtensions;

    private readonly string _root;
    public FileStorage(IWebHostEnvironment env)
    {
        _root = Path.Combine(env.ContentRootPath, "Uploads");
        Directory.CreateDirectory(_root);
    }

    // Hata varsa mesajı döner, yoksa null
    public static string? Validate(IList<IFormFile>? files)
    {
        if (files == null || files.Count == 0) return null;
        if (files.Count > MaxFiles) return $"En fazla {MaxFiles} dosya yükleyebilirsiniz.";
        foreach (var f in files)
        {
            var name = UploadPolicy.SafeFileName(f.FileName);
            if (f.Length == 0) return $"\"{name}\" boş bir dosya.";
            if (f.Length > MaxBytes) return $"\"{name}\" 10 MB sınırını aşıyor.";

            using var stream = f.OpenReadStream();
            if (UploadPolicy.Check(f.FileName, f.ContentType, stream) is { } error) return error;
        }
        return null;
    }

    public async Task<List<Attachment>> SaveAsync(IList<IFormFile>? files)
    {
        var list = new List<Attachment>();
        if (files == null) return list;

        foreach (var f in files.Where(f => f.Length > 0))
        {
            var name = UploadPolicy.SafeFileName(f.FileName);
            var ext = Path.GetExtension(name).ToLowerInvariant();

            // Çağıran Validate'i atlamış olsa bile izin listesi dışı dosya diske yazılmaz
            if (!UploadPolicy.IsAllowedExtension(ext)) continue;

            var stored = Guid.NewGuid().ToString("N") + ext;
            await using var stream = File.Create(Path.Combine(_root, stored));
            await f.CopyToAsync(stream);

            list.Add(new Attachment
            {
                FileName = name,
                StoredFileName = stored,
                // İçerik türü istemcinin beyanından değil, doğrulanmış uzantıdan gelir
                ContentType = UploadPolicy.ContentTypeFor(stored),
                Size = f.Length
            });
        }
        return list;
    }

    public string PathOf(Attachment a) => Path.Combine(_root, a.StoredFileName);
}
