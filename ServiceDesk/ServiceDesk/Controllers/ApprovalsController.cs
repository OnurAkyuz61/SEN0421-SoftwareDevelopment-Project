using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// YÖNETİM: Değişiklik taleplerinin onay / red işlemleri
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class ApprovalsController : Controller
{
    private readonly AppDbContext _db;
    public ApprovalsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        ViewBag.History = await _db.Tickets
            .Where(t => t.ApprovalStatus == ApprovalStatus.Approved || t.ApprovalStatus == ApprovalStatus.Rejected)
            .OrderByDescending(t => t.ApprovalDecidedAt).Take(10).ToListAsync();

        return View(await _db.Tickets
            .Where(t => t.ApprovalStatus == ApprovalStatus.Pending)
            .OrderBy(t => t.CreatedAt).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(int id, string? note, string? returnUrl) => Decide(id, true, note, returnUrl);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(int id, string? note, string? returnUrl) => Decide(id, false, note, returnUrl);

    private async Task<IActionResult> Decide(int id, bool approve, string? note, string? returnUrl)
    {
        var t = await _db.Tickets.FindAsync(id);
        if (t == null) return NotFound();

        // Başka bir yönetici bu arada karar vermişse ikinci karar yazılmaz
        if (t.ApprovalStatus != ApprovalStatus.Pending)
        {
            TempData["Err"] = $"#{t.Id} zaten karara bağlanmış ({Ui.ApprovalText(t.ApprovalStatus)}).";
            return RedirectToAction(nameof(Index));
        }

        t.ApprovalStatus = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        t.ApprovalNote = note;
        t.ApprovalDecidedAt = AppTime.UtcNow;
        if (!approve)
        {
            t.Status = TicketStatus.Closed;      // reddedilen talep kapanır
            t.ResolvedAt ??= AppTime.UtcNow;
            t.ClosedAt ??= AppTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        TempData["Ok"] = $"#{t.Id} {(approve ? "onaylandı" : "reddedildi")}.";
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
    }
}
