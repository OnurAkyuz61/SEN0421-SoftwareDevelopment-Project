using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// Ek dosyayı açar / indirir ve kaldırır:
//   GET   /Attachments/Get/5
//   POST  /Attachments/Delete/5
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
            // IgnoreQueryFilters ekin kendi yumuşak silmesini de kaldırdığı için
            // koşul elle yazılır: kaldırılmış dosya indirilemez
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (a == null) return NotFound();

        // Yetki kuralı AttachmentPolicy'de (birim testi yazılabilsin diye saf sınıf).
        // Yetkisiz erişimde 404 döner: ek numarasını deneyerek varlık yoklaması yapılamasın.
        if (!AttachmentPolicy.CanView(a, _current.User!)) return NotFound();

        // Antivirüs taraması (IAttachmentScanner) temiz sonuçlanmadan dosya
        // servis edilmez. Burada 404 yerine açıklayıcı yanıt dönüyoruz: dosyanın
        // varlığı kullanıcıya zaten görünüyor (ek listesinde duruyor), gizlenecek
        // bir şey yok; gizlenirse "dosyam kayboldu" diye destek talebi açılır.
        if (!AttachmentPolicy.IsServable(a))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Content(a.ScanStatus == AttachmentScanStatus.Suspicious
                ? "Bu dosya güvenlik taramasında şüpheli bulundu; indirilemez. Destek ekibiyle görüşün."
                : "Bu dosya henüz güvenlik taramasından geçmedi. Lütfen birkaç dakika sonra yeniden deneyin.",
                "text/plain; charset=utf-8");
        }

        var path = _files.PathOf(a);
        // StoredFileName guid + uzantıdır; yine de Uploads dışına çıkan bir yol reddedilir
        if (Path.GetFileName(a.StoredFileName) != a.StoredFileName || !System.IO.File.Exists(path))
            return NotFound();

        // Güvenli servis kuralları FileServing'de: uygunsuzluk kanıtı da
        // (NonconformitiesController.Evidence) aynı başlıklardan geçiyor
        var contentType = UploadPolicy.ContentTypeFor(a.StoredFileName);
        FileServing.ApplySafeHeaders(Response);

        if (!download && FileServing.CanShowInline(path, a.StoredFileName))
        {
            FileServing.SetInlineFileName(Response, a.FileName);
            return PhysicalFile(path, contentType);
        }

        // İndirme: imzası tutmayan "görsel" de dahil, tarayıcı içeriği yorumlamaz
        return PhysicalFile(path, FileServing.DownloadType(contentType), UploadPolicy.SafeFileName(a.FileName));
    }

    // Ek dosyayı kaldırır. Kayıt SİLİNMEZ, işaretlenir (ISO 9001 md. 7.5:
    // kayıtların korunması): dosya diskte kalır, kimin ne zaman kaldırdığı
    // kayıtlıdır, ama listelere ve indirmeye düşmez (global query filter).
    //
    // Yetki kuralı AttachmentPolicy.CanDelete'te (birim testi yazılabilsin diye
    // saf sınıf). Sorgu global filtreyle kurulduğu için zaten kaldırılmış ek ve
    // arşivlenmiş talebin eki buraya hiç gelmez.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnTo)
    {
        var a = await _db.Attachments
            .Include(x => x.Ticket)
            .Include(x => x.Comment)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();

        var me = _current.User!;

        // Göremediği ek "bulunamadı" sayılır — Get ucuyla aynı kural: ek
        // numarası denenerek başkasının dosyasının varlığı yoklanamaz.
        if (!AttachmentPolicy.CanView(a, me)) return NotFound();

        // Görüyor ama kaldıramıyor (başkasının yüklediği dosya, kapanmış talep):
        // dosya ekranında durduğu için burada açık mesaj veriyoruz.
        if (!AttachmentPolicy.CanDelete(a, me))
        {
            TempData["Err"] = "Bu dosyayı kaldırma yetkiniz yok.";
            return BackToTicket(a.TicketId, returnTo);
        }

        a.IsDeleted = true;
        a.DeletedAt = AppTime.UtcNow;
        a.DeletedById = me.Id;

        // Denetim izi: dosyanın kaldırılması talep geçmişine düşer
        _db.TicketHistories.Add(new TicketHistory
        {
            TicketId = a.TicketId,
            Field = "Ek dosya",
            OldValue = a.FileName,
            NewValue = "kaldırıldı",
            ChangedById = me.Id,
            ChangedAt = AppTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Ok"] = $"\"{a.FileName}\" kaldırıldı.";
        return BackToTicket(a.TicketId, returnTo);
    }

    // Son kullanıcı portal detayına, destek / yönetici talep detayına döner.
    // Portal popup'ından gelindiyse liste yeniden aynı talebi açar.
    private IActionResult BackToTicket(int ticketId, string? returnTo)
    {
        if (string.Equals(returnTo, "modal", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "Portal", new { open = ticketId });

        return _current.User!.Role == UserRole.User
            ? RedirectToAction("Details", "Portal", new { id = ticketId })
            : RedirectToAction("Details", "Tickets", new { id = ticketId });
    }

}
