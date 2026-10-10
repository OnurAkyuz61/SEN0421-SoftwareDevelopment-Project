using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Controllers;

public class TicketsController : Controller
{
    private readonly AppDbContext _db;
    public TicketsController(AppDbContext db) => _db = db;

    // Filtreler: arama metni, durum, öncelik, tür
    public async Task<IActionResult> Index(string? q, TicketStatus? status, Priority? priority, TicketType? type)
    {
        var query = _db.Tickets.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(t => t.Title.Contains(q) || t.RequesterName.Contains(q) || (t.AssignedTo != null && t.AssignedTo.Contains(q)));
        if (status != null) query = query.Where(t => t.Status == status);
        if (priority != null) query = query.Where(t => t.Priority == priority);
        if (type != null) query = query.Where(t => t.Type == type);

        ViewBag.Q = q; ViewBag.Status = status; ViewBag.Priority = priority; ViewBag.Type = type;

        var list = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

        // Basit eskalasyon: SLA aşılan açık talepler işaretlenir
        foreach (var t in list.Where(t => t.IsSlaBreached && t.ResolvedAt == null && !t.IsEscalated))
            t.IsEscalated = true;
        await _db.SaveChangesAsync();

        return View(list);
    }

    public IActionResult Create() => View(new Ticket());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Ticket ticket)
    {
        if (!ModelState.IsValid) return View(ticket);

        var sla = await _db.SlaPolicies.FirstAsync(s => s.Priority == ticket.Priority);
        ticket.CreatedAt = DateTime.Now;
        ticket.SlaDueAt = ticket.CreatedAt.AddHours(sla.ResolutionHours);

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var ticket = await _db.Tickets.Include(t => t.Feedback).FirstOrDefaultAsync(t => t.Id == id);
        return ticket == null ? NotFound() : View(ticket);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, TicketStatus status, string? assignedTo)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        ticket.Status = status;
        ticket.AssignedTo = assignedTo;
        if (status is TicketStatus.Resolved or TicketStatus.Closed)
            ticket.ResolvedAt ??= DateTime.Now;
        else
            ticket.ResolvedAt = null;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFeedback(int id, int score, string? comment)
    {
        var exists = await _db.Feedbacks.AnyAsync(f => f.TicketId == id);
        if (!exists && score is >= 1 and <= 5)
        {
            _db.Feedbacks.Add(new Feedback { TicketId = id, Score = score, Comment = comment });
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
