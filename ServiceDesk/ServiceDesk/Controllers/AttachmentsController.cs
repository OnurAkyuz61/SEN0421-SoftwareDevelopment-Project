using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// Ek dosyayı açar / indirir: /Attachments/Get/5
[AuthorizeRole]
public class AttachmentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly FileStorage _files;
    private readonly CurrentUser _current;

    public AttachmentsController(AppDbContext db, FileStorage files, CurrentUser current)
    {
        _db = db;
        _files = files;
        _current = current;
    }

    // Yetkili kullanıcıya bile dosya "güvenli" açılır:
    //  • İçerik türü veritabanındaki (eskiden istemcinin bildirdiği) değerden
    //    değil, diskteki dosyanın uzantısından türetilir.
    //  • Yalnızca PNG/JPEG/GIF/WEBP tarayıcıda açılır ve açılmadan önce dosyanın
    //    imzası bir kez daha kontrol edilir (bu doğrulama gelmeden önce yüklenmiş
    //    dosyalar için). İmza tutmazsa dosya görsel gibi gösterilmez, indirilir.
    //  • Diğer her şey (PDF, Office, metin, zip) her zaman indirme olarak gelir.
    //  • nosniff: tarayıcı içeriğe bakıp türü kendisi "tahmin" edemez.
    //  • CSP sandbox: görsel yeni sekmede doğrudan açılsa bile sayfa bağlamında
    //    hiçbir betik çalışamaz, form gönderemez, çerezlere erişemez.
    //  • Önbellek yok: oturum kapandıktan sonra ortak bilgisayarda kalmasın.
    public async Task<IActionResult> Get(int id, bool download = false)
    {
        // Mesaj da yükleniyor: iç nota eklenmiş dosya talep sahibine kapalıdır.
        // Arşivlenmiş talebin eki de destek ekibinin arşiv detayından açılabilsin
        // diye filtre yok sayılır; son kullanıcıya AttachmentPolicy engeller.
        var a = await _db.Attachments.AsNoTracking()
            .IgnoreQueryFilters()
            .Include(x => x.Ticket)
            .Include(x => x.Comment)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();

        // Yetki kuralı AttachmentPolicy'de (birim testi yazılabilsin diye saf sınıf).
        // Yetkisiz erişimde 404 döner: ek numarasını deneyerek varlık yoklaması yapılamasın.
        if (!AttachmentPolicy.CanView(a, _current.User!)) return NotFound();

        var path = _files.PathOf(a);
        // StoredFileName guid + uzantıdır; yine de Uploads dışına çıkan bir yol reddedilir
        if (Path.GetFileName(a.StoredFileName) != a.StoredFileName || !System.IO.File.Exists(path))
            return NotFound();

        var contentType = UploadPolicy.ContentTypeFor(a.StoredFileName);
        var inline = !download && UploadPolicy.IsInlineImage(a.StoredFileName) && ImageSignatureMatches(path, a.StoredFileName);

        var h = Response.Headers;
        h[HeaderNames.XContentTypeOptions] = "nosniff";
        h[HeaderNames.ContentSecurityPolicy] = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";
        h["Cross-Origin-Resource-Policy"] = "same-origin";
        h[HeaderNames.CacheControl] = "private, no-store";
        h["Referrer-Policy"] = "no-referrer";

        if (inline)
        {
            // inline + güvenli ad: "Farklı kaydet" de doğru adı önerir
            var cd = new ContentDispositionHeaderValue("inline");
            cd.SetHttpFileName(a.FileName);
            h[HeaderNames.ContentDisposition] = cd.ToString();
            return PhysicalFile(path, contentType);
        }

        // İndirme: imzası tutmayan "görsel" de dahil, tarayıcı içeriği yorumlamaz
        return PhysicalFile(path, DownloadType(contentType), UploadPolicy.SafeFileName(a.FileName));
    }

    // Görsel olmayan türler olduğu gibi; imzası tutmayan görsel genel ikili tür olarak iner
    private static string DownloadType(string contentType) =>
        contentType.StartsWith("image/", StringComparison.Ordinal) ? "application/octet-stream" : contentType;

    private static bool ImageSignatureMatches(string path, string storedName)
    {
        try
        {
            using var fs = System.IO.File.OpenRead(path);
            return UploadPolicy.Check(storedName, null, fs) == null;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
