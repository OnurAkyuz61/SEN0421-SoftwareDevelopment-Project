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
        var a = await _db.Attachments.Include(x => x.Ticket).FirstOrDefaultAsync(x => x.Id == id);
        if (a == null || !System.IO.File.Exists(_files.PathOf(a))) return NotFound();

        // Destek ve yönetici her eke erişir; son kullanıcı yalnızca kendi talebinin ekine.
        // Yetkisiz erişimde 404 döner: ek numarasını deneyerek varlık yoklaması yapılamasın.
        if (!_current.IsStaff && a.Ticket!.RequesterId != _current.User!.Id) return NotFound();

        // Resimler tarayıcıda açılır, diğerleri indirilir
        return a.IsImage && !download
            ? PhysicalFile(_files.PathOf(a), a.ContentType)
            : PhysicalFile(_files.PathOf(a), a.ContentType, a.FileName);
    }
}
