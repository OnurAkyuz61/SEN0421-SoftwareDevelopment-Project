using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Data;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// Ek dosyayı açar / indirir: /Attachments/Get/5
public class AttachmentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly FileStorage _files;
    public AttachmentsController(AppDbContext db, FileStorage files) { _db = db; _files = files; }

    public async Task<IActionResult> Get(int id, bool download = false)
    {
        var a = await _db.Attachments.FindAsync(id);
        if (a == null || !System.IO.File.Exists(_files.PathOf(a))) return NotFound();

        // Resimler tarayıcıda açılır, diğerleri indirilir
        return a.IsImage && !download
            ? PhysicalFile(_files.PathOf(a), a.ContentType)
            : PhysicalFile(_files.PathOf(a), a.ContentType, a.FileName);
    }
}
