using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// KULLANICI PORTALI: talep açma, dosya yükleme, kendi taleplerini takip
// TODO (login): oturumdaki kullanıcıya göre filtrele ve RequesterId/RequesterName'i otomatik doldur
public class PortalController : Controller
{
    private readonly AppDbContext _db;
    private readonly FileStorage _files;
    public PortalController(AppDbContext db, FileStorage files) { _db = db; _files = files; }

    public async Task<IActionResult> Index(string? name)
    {
        var query = _db.Tickets.AsQueryable();
        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(t => t.RequesterName.Contains(name));

        ViewBag.Name = name;
        return View(await query.OrderByDescending(t => t.CreatedAt).ToListAsync());
    }

    public IActionResult Create() => View(new Ticket { Type = TicketType.Incident });

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> Create(
        [Bind("Title,Description,Type,Priority,Category,RequesterName")] Ticket ticket,
        List<IFormFile>? files)
    {
        var fileError = FileStorage.Validate(files);
        if (fileError != null) ModelState.AddModelError("files", fileError);
        if (!ModelState.IsValid) return View(ticket);

        var sla = await _db.SlaPolicies.FirstAsync(s => s.Priority == ticket.Priority);
        ticket.CreatedAt = DateTime.Now;
        ticket.SlaDueAt = ticket.CreatedAt.AddHours(sla.ResolutionHours);
        ticket.Status = TicketStatus.New;
        // Değişiklik talepleri yönetici onayına düşer
        ticket.ApprovalStatus = ticket.Type == TicketType.Change ? ApprovalStatus.Pending : ApprovalStatus.NotRequired;
        ticket.Attachments = await _files.SaveAsync(files);

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        TempData["Ok"] = $"Talebiniz alındı. Talep numaranız: #{ticket.Id}";
        return RedirectToAction(nameof(Details), new { id = ticket.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Feedback)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);
        return ticket == null ? NotFound() : View(ticket);
    }

    // Açılmış bir talebe sonradan dosya / ekran görüntüsü eklemek için
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> AddFiles(int id, List<IFormFile>? files)
    {
        var ticket = await _db.Tickets.Include(t => t.Attachments).FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();

        var err = FileStorage.Validate(files);
        if (err != null) TempData["Err"] = err;
        else if (files is { Count: > 0 })
        {
            ticket.Attachments.AddRange(await _files.SaveAsync(files));
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Dosyalar eklendi.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Feedback(int id, int score, string? comment)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        var exists = await _db.Feedbacks.AnyAsync(f => f.TicketId == id);
        if (ticket != null && !exists && score is >= 1 and <= 5
            && ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            _db.Feedbacks.Add(new Feedback { TicketId = id, Score = score, Comment = comment });
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Değerlendirmeniz için teşekkürler.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
