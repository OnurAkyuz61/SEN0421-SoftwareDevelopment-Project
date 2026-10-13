using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Yüklenen dosyaları wwwroot DIŞINDA (Uploads/) saklar; böylece link bilen herkes indiremez
public class FileStorage
{
    public const int MaxFiles = 5;
    public const long MaxBytes = 10 * 1024 * 1024; // 10 MB
    public static readonly string[] AllowedExt =
        { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".log", ".zip" };

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
            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
            if (!AllowedExt.Contains(ext)) return $"\"{f.FileName}\" türü desteklenmiyor.";
            if (f.Length > MaxBytes) return $"\"{f.FileName}\" 10 MB sınırını aşıyor.";
        }
        return null;
    }

    public async Task<List<Attachment>> SaveAsync(IList<IFormFile>? files)
    {
        var list = new List<Attachment>();
        if (files == null) return list;

        foreach (var f in files.Where(f => f.Length > 0))
        {
            var stored = Guid.NewGuid().ToString("N") + Path.GetExtension(f.FileName).ToLowerInvariant();
            await using var stream = File.Create(Path.Combine(_root, stored));
            await f.CopyToAsync(stream);

            list.Add(new Attachment
            {
                FileName = Path.GetFileName(f.FileName),
                StoredFileName = stored,
                ContentType = string.IsNullOrEmpty(f.ContentType) ? "application/octet-stream" : f.ContentType,
                Size = f.Length
            });
        }
        return list;
    }

    public string PathOf(Attachment a) => Path.Combine(_root, a.StoredFileName);
}
