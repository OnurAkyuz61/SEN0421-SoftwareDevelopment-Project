using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public async Task<IActionResult> Get(int id, bool download = false)
    {
        // Mesaj da yükleniyor: iç nota eklenmiş dosya talep sahibine kapalıdır.
        // Arşivlenmiş (soft-delete) talebin eki global filtre sayesinde
        // sorguya hiç düşmez.
        var a = await _db.Attachments.AsNoTracking()
            .Include(x => x.Ticket)
            .Include(x => x.Comment)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (a == null || !System.IO.File.Exists(_files.PathOf(a))) return NotFound();

        // Yetki kuralı AttachmentPolicy'de (birim testi yazılabilsin diye saf sınıf).
        // Yetkisiz erişimde 404 döner: ek numarasını deneyerek varlık yoklaması yapılamasın.
        if (!AttachmentPolicy.CanView(a, _current.User!)) return NotFound();

        // Resimler tarayıcıda açılır, diğerleri indirilir
        return a.IsImage && !download
            ? PhysicalFile(_files.PathOf(a), a.ContentType)
            : PhysicalFile(_files.PathOf(a), a.ContentType, a.FileName);
    }
}
