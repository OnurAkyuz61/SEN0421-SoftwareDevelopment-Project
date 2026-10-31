using System.IO.Compression;
using System.Text;

namespace ServiceDesk.Services;

// Yüklenen dosyanın gerçekten söylediği şey olup olmadığını denetler.
//
// Önceki kontrol yalnızca uzantıya bakıyordu ve tarayıcının bildirdiği içerik
// türünü (Content-Type) olduğu gibi kaydediyordu. Bu iki açık bırakıyordu:
//   • "fatura.pdf" adıyla yüklenen bir .exe kabul ediliyordu (uzantı tutuyor).
//   • İçerik türü istemciden geldiği için ".png" uzantılı ama "text/html"
//     bildirilen bir dosya, görsel diye açıldığında tarayıcıda HTML/JS olarak
//     çalışabiliyordu (stored XSS).
//
// Artık üç katlı doğrulama var:
//   1. Uzantı izin listesinde olmalı.
//   2. Tarayıcının bildirdiği içerik türü bu uzantıyla uyumlu olmalı
//      (bilinmiyorsa "application/octet-stream" ya da boş kabul edilir; bazı
//      işletim sistemleri .log, .docx için tür bildirmez).
//   3. Dosyanın ilk baytları (imza / "magic number") uzantıyla eşleşmeli;
//      Office dosyalarında arşivin içi de kontrol edilir, metin dosyalarında
//      ikili içerik aranır.
// Kaydedilen içerik türü HİÇBİR ZAMAN istemciden alınmaz; uzantıdan türetilir.
//
// Saf sınıf: birim testi yazılabiliyor (bkz. ServiceDesk.Tests).
public static class UploadPolicy
{
    private sealed record Rule(string ContentType, string[] AcceptedDeclaredTypes, Func<Stream, bool> Matches);

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] Gif87 = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89 = "GIF89a"u8.ToArray();
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Zip = { 0x50, 0x4B, 0x03, 0x04 };
    private static readonly byte[] ZipEmpty = { 0x50, 0x4B, 0x05, 0x06 };
    private static readonly byte[] Ole = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
    private static readonly byte[] Elf = { 0x7F, 0x45, 0x4C, 0x46 };

    private static readonly Dictionary<string, Rule> Rules = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"]  = new("image/png",  new[] { "image/png" }, s => StartsWith(s, Png)),
        [".jpg"]  = new("image/jpeg", new[] { "image/jpeg", "image/pjpeg" }, s => StartsWith(s, Jpeg)),
        [".jpeg"] = new("image/jpeg", new[] { "image/jpeg", "image/pjpeg" }, s => StartsWith(s, Jpeg)),
        [".gif"]  = new("image/gif",  new[] { "image/gif" }, s => StartsWith(s, Gif87) || StartsWith(s, Gif89)),
        [".webp"] = new("image/webp", new[] { "image/webp" }, s => StartsWith(s, Riff) && StartsWith(s, Webp, 8)),
        [".pdf"]  = new("application/pdf", new[] { "application/pdf", "application/x-pdf" }, s => StartsWith(s, Pdf)),
        [".doc"]  = new("application/msword", new[] { "application/msword" }, s => StartsWith(s, Ole)),
        [".xls"]  = new("application/vnd.ms-excel", new[] { "application/vnd.ms-excel" }, s => StartsWith(s, Ole)),
        [".docx"] = new("application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/zip" },
                        s => IsOfficeOpenXml(s, "word/")),
        [".xlsx"] = new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/zip" },
                        s => IsOfficeOpenXml(s, "xl/")),
        [".zip"]  = new("application/zip",
                        new[] { "application/zip", "application/x-zip-compressed", "application/x-zip", "multipart/x-zip" },
                        s => StartsWith(s, Zip) || StartsWith(s, ZipEmpty)),
        [".txt"]  = new("text/plain", new[] { "text/plain" }, IsPlainText),
        [".log"]  = new("text/plain", new[] { "text/plain", "text/x-log" }, IsPlainText),
    };

    // Tarayıcıda (img etiketiyle ya da yeni sekmede) açılmasına izin verilen
    // görseller. SVG bilerek yok: içinde JavaScript taşıyabilir.
    private static readonly HashSet<string> InlineImageTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/png", "image/jpeg", "image/gif", "image/webp" };

    // Bazı tarayıcı / işletim sistemi ikilileri tür bildirmez ya da genel tür gönderir
    private static readonly HashSet<string> GenericDeclaredTypes = new(StringComparer.OrdinalIgnoreCase)
        { "", "application/octet-stream", "binary/octet-stream", "application/unknown" };

    public static IReadOnlyCollection<string> AllowedExtensions => Rules.Keys;

    public static bool IsAllowedExtension(string ext) => Rules.ContainsKey(ext);

    // Diskteki adın uzantısından güvenli içerik türü; bilinmeyen uzantıda indirme türü
    public static string ContentTypeFor(string fileName) =>
        Rules.TryGetValue(Path.GetExtension(fileName), out var r) ? r.ContentType : "application/octet-stream";

    public static bool IsInlineImage(string fileName) => InlineImageTypes.Contains(ContentTypeFor(fileName));

    // Hata varsa kullanıcıya gösterilecek mesajı döner, yoksa null.
    // content okunabilir ve konumlanabilir (seekable) olmalı; konumu geri alınır.
    public static string? Check(string fileName, string? declaredContentType, Stream content)
    {
        var shown = SafeFileName(fileName);
        var ext = Path.GetExtension(shown);

        if (!Rules.TryGetValue(ext, out var rule))
            return $"\"{shown}\" türü desteklenmiyor.";

        var declared = (declaredContentType ?? "").Split(';')[0].Trim();
        if (!GenericDeclaredTypes.Contains(declared) &&
            !rule.AcceptedDeclaredTypes.Contains(declared, StringComparer.OrdinalIgnoreCase))
            return $"\"{shown}\" dosyasının içerik türü ({declared}) uzantısıyla uyuşmuyor.";

        bool ok;
        var start = content.CanSeek ? content.Position : 0;
        try
        {
            ok = rule.Matches(content);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        {
            ok = false;
        }
        finally
        {
            if (content.CanSeek) content.Position = start;
        }

        return ok ? null : $"\"{shown}\" dosyasının içeriği {ext} biçiminde değil; dosya bozuk ya da uzantısı değiştirilmiş olabilir.";
    }

    // Kullanıcının dosya adı yalnızca gösterim ve indirme adı olarak kullanılır
    // (diskte guid saklanır). Yine de yol parçaları, kontrol karakterleri ve
    // tarayıcının indirme penceresini şaşırtan karakterler temizlenir.
    public static string SafeFileName(string? raw)
    {
        var name = Path.GetFileName((raw ?? "").Replace('\\', '/'));
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (char.IsControl(ch) || ch is '"' or '<' or '>' or '|' or ':' or '*' or '?' or '/' or '\\'
                || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.Format) // RTL override vb.
                continue;
            sb.Append(ch);
        }
        name = sb.ToString().Trim().TrimEnd('.');
        if (name.Length == 0 || name.StartsWith('.')) name = "dosya" + name;
        if (name.Length > 200)
        {
            var e = Path.GetExtension(name);
            name = name[..(200 - e.Length)] + e;
        }
        return name;
    }

    // ───── İmza kontrolleri ──────────────────────────────────────────────

    private static bool StartsWith(Stream s, byte[] sig, int offset = 0)
    {
        if (s.CanSeek) s.Position = offset;
        else if (offset > 0) return false;
        var buf = new byte[sig.Length];
        var read = 0;
        while (read < buf.Length)
        {
            var n = s.Read(buf, read, buf.Length - read);
            if (n == 0) return false;
            read += n;
        }
        if (s.CanSeek) s.Position = 0;
        return buf.AsSpan().SequenceEqual(sig);
    }

    // .docx / .xlsx bir zip arşividir; içinde [Content_Types].xml ve ilgili
    // klasör (word/ ya da xl/) bulunmalı. Adı .docx yapılmış rastgele bir zip
    // (ya da içine çalıştırılabilir dosya konmuş arşiv) böylece ayrılır.
    private static bool IsOfficeOpenXml(Stream s, string partPrefix)
    {
        if (!StartsWith(s, Zip)) return false;
        using var zip = new ZipArchive(s, ZipArchiveMode.Read, leaveOpen: true);
        return zip.Entries.Any(e => e.FullName == "[Content_Types].xml")
            && zip.Entries.Any(e => e.FullName.StartsWith(partPrefix, StringComparison.Ordinal));
    }

    // Metin dosyası: ilk 8 KB'ta NUL baytı olmamalı ve bilinen bir ikili
    // biçimle (exe, zip, pdf, görsel) başlamamalı. UTF-8 / UTF-16 BOM'lu
    // dosyalar metin sayılır.
    private static bool IsPlainText(Stream s)
    {
        if (s.CanSeek) s.Position = 0;
        var buf = new byte[8192];
        var n = s.Read(buf, 0, buf.Length);
        var head = buf.AsSpan(0, n);

        if (head.StartsWith(new byte[] { 0xFF, 0xFE }) || head.StartsWith(new byte[] { 0xFE, 0xFF }))
            return true; // UTF-16: NUL baytı doğal olarak bulunur

        if (head.StartsWith("MZ"u8) || head.StartsWith(Elf)
            || head.StartsWith(Zip) || head.StartsWith(Pdf) || head.StartsWith(Png) || head.StartsWith(Ole))
            return false;

        return head.IndexOf((byte)0) < 0;
    }
}
