using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// YÖNETİM: Uygunsuzluk ve düzeltici faaliyet kayıtları (ISO 9001 md. 10.2)
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class NonconformitiesController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUser _current;

    public NonconformitiesController(AppDbContext db, CurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IActionResult> Index(NonconformityStatus? status)
    {
        var q = _db.Nonconformities
            .AsNoTracking()
            .Include(n => n.Ticket)
            .Include(n => n.Responsible)
            .AsQueryable();

        if (status != null) q = q.Where(n => n.Status == status);

        ViewBag.Status = status;
        ViewBag.OpenCount = await _db.Nonconformities.CountAsync(n => n.Status != NonconformityStatus.Closed);
        ViewBag.Staff = await _db.Users.AsNoTracking()
            .Where(u => u.Role != UserRole.User && u.IsActive)
            .OrderBy(u => u.FirstName).ToListAsync();

        return View(await q
            .OrderBy(n => n.Status)
            .ThenBy(n => n.DueAt)
            .ToListAsync());
    }

    // Kök neden, düzeltici faaliyet, sorumlu ve etkinlik doğrulaması
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        int id, string? rootCause, string? correctiveAction, int? responsibleId,
        DateTime? dueAt, NonconformityStatus status, bool? effectivenessVerified)
    {
        var n = await _db.Nonconformities.FindAsync(id);
        if (n == null) return NotFound();

        n.RootCause = rootCause;
        n.CorrectiveAction = correctiveAction;
        n.ResponsibleId = responsibleId;
        n.DueAt = dueAt == null ? null : AppTime.ToUtc(dueAt.Value);
        n.EffectivenessVerified = effectivenessVerified;

        // Kapatmak için kök neden ve düzeltici faaliyet zorunlu
        if (status == NonconformityStatus.Closed &&
            (string.IsNullOrWhiteSpace(rootCause) || string.IsNullOrWhiteSpace(correctiveAction)))
        {
            TempData["Err"] = "Kök neden ve düzeltici faaliyet yazılmadan uygunsuzluk kapatılamaz.";
            n.Status = n.Status == NonconformityStatus.Closed ? NonconformityStatus.InProgress : n.Status;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        n.Status = status;
        n.ClosedAt = status == NonconformityStatus.Closed ? (n.ClosedAt ?? AppTime.UtcNow) : null;

        await _db.SaveChangesAsync();

        TempData["Ok"] = $"Uygunsuzluk #{n.Id} güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    // Denetimde veya gözlemle tespit edilen uygunsuzluk elle açılır
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string? description, string? ticketNo)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            TempData["Err"] = "Uygunsuzluk açıklaması boş olamaz.";
            return RedirectToAction(nameof(Index));
        }

        // Talep no opsiyonel; yazıldıysa böyle bir talep gerçekten olmalı.
        // "INC-2026-00042" gibi numara ya da "42" / "#42" gibi kayıt Id'si kabul edilir.
        int? ticketId = null;
        if (!string.IsNullOrWhiteSpace(ticketNo))
        {
            var no = ticketNo.Trim();
            int.TryParse(no.TrimStart('#'), out var idNo);

            var ticket = await _db.Tickets.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Number == no || t.Id == idNo);
            if (ticket == null)
            {
                TempData["Err"] = $"\"{no}\" numaralı talep bulunamadı.";
                return RedirectToAction(nameof(Index));
            }
            ticketId = ticket.Id;
        }

        _db.Nonconformities.Add(new Nonconformity
        {
            TicketId = ticketId,
            Source = NonconformitySource.Manual,
            Status = NonconformityStatus.Open,
            Description = description.Trim(),
            ResponsibleId = _current.User!.Id,
            DetectedAt = AppTime.UtcNow,
            DueAt = AppTime.UtcNow.AddDays(7)
        });

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Uygunsuzluk kaydı açıldı.";
        return RedirectToAction(nameof(Index));
    }
}
