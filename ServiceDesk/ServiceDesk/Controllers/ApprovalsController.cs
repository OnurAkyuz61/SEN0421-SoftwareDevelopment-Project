using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Controllers;

// YÖNETİM: Değişiklik taleplerinin onay / red işlemleri
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
        if (t == null || t.ApprovalStatus != ApprovalStatus.Pending) return NotFound();

        t.ApprovalStatus = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        t.ApprovalNote = note;
        t.ApprovalDecidedAt = DateTime.Now;
        if (!approve)
        {
            t.Status = TicketStatus.Closed;      // reddedilen talep kapanır
            t.ResolvedAt = DateTime.Now;
        }
        await _db.SaveChangesAsync();

        TempData["Ok"] = $"#{t.Id} {(approve ? "onaylandı" : "reddedildi")}.";
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
    }
}
