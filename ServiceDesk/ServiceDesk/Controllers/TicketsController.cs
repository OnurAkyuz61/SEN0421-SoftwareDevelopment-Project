using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Controllers;

// YÖNETİM: tüm talepleri görme, atama, durum güncelleme
// (Yeni talep açma artık Kullanıcı Portalında: /Portal/Create)
public class TicketsController : Controller
{
    private readonly AppDbContext _db;
    public TicketsController(AppDbContext db) => _db = db;

    // Filtreler: arama metni, durum, öncelik, tür, kategori
    public async Task<IActionResult> Index(string? q, TicketStatus? status, Priority? priority, TicketType? type, TicketCategory? category)
    {
        var query = _db.Tickets.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(t => t.Title.Contains(q) || t.RequesterName.Contains(q) || (t.AssignedTo != null && t.AssignedTo.Contains(q)));
        if (status != null) query = query.Where(t => t.Status == status);
        if (priority != null) query = query.Where(t => t.Priority == priority);
        if (type != null) query = query.Where(t => t.Type == type);
        if (category != null) query = query.Where(t => t.Category == category);

        ViewBag.Q = q; ViewBag.Status = status; ViewBag.Priority = priority; ViewBag.Type = type; ViewBag.Category = category;

        var list = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

        // Basit eskalasyon: SLA aşılan açık talepler işaretlenir
        foreach (var t in list.Where(t => t.IsSlaBreached && t.ResolvedAt == null && !t.IsEscalated))
            t.IsEscalated = true;
        await _db.SaveChangesAsync();

        return View(list);
    }

    public async Task<IActionResult> Details(int id)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Feedback)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();

        // Atama listesi: Destek ve Yönetici rolündeki kullanıcılar
        var staff = await _db.Users.Where(u => u.Role != UserRole.User).ToListAsync();
        ViewBag.Staff = staff.Select(u => u.FullName).ToList();
        return View(ticket);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, TicketStatus status, string? assignedTo)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        // Onaylanmamış değişiklik talebi üzerinde çalışılamaz (Change Management kuralı)
        if (ticket.Type == TicketType.Change && ticket.ApprovalStatus != ApprovalStatus.Approved
            && status is TicketStatus.InProgress or TicketStatus.Resolved)
        {
            TempData["Err"] = "Bu değişiklik talebi onaylanmadan işleme alınamaz.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ticket.Status = status;
        ticket.AssignedTo = string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo;
        if (status is TicketStatus.Resolved or TicketStatus.Closed)
            ticket.ResolvedAt ??= DateTime.Now;
        else
            ticket.ResolvedAt = null;

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Talep güncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
