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
    private readonly NotificationService _notify;
    private readonly CurrentUser _current;

    public ApprovalsController(AppDbContext db, NotificationService notify, CurrentUser current)
    {
        _db = db;
        _notify = notify;
        _current = current;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.History = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.ApprovalStatus == ApprovalStatus.Approved || t.ApprovalStatus == ApprovalStatus.Rejected)
            .OrderByDescending(t => t.ApprovalDecidedAt).Take(10).ToListAsync();

        return View(await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.ChangeDetail)
            .Include(t => t.Approvals).ThenInclude(a => a.Approver)
            .Where(t => t.ApprovalStatus == ApprovalStatus.Pending)
            .OrderBy(t => t.CreatedAt).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(int id, string? note, string? returnUrl) => Decide(id, true, note, returnUrl);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(int id, string? note, string? returnUrl) => Decide(id, false, note, returnUrl);

    private async Task<IActionResult> Decide(int id, bool approve, string? note, string? returnUrl)
    {
        var t = await _db.Tickets
            .Include(x => x.ChangeDetail)
            .Include(x => x.Approvals)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();

        var me = _current.User!;

        // Yetki kuralları tek yerde: ApprovalPolicy (talep türü, rol, kendi
        // talebi, nihai karar yetkisi ve zincirde tekrar onay kontrolü).
        // Ekrandaki düğmeler de aynı kuralla gösterilip gizleniyor; bu kontrol
        // adresin doğrudan POST edilmesine karşı asıl engeldir.
        var yetki = ApprovalPolicy.CanDecide(t, me, approve);
        if (!yetki.Allowed)
        {
            TempData["Err"] = yetki.Error;
            return RedirectToAction(nameof(Index));
        }

        var step = ApprovalPolicy.NextStep(t);
        var required = t.RequiredApprovalSteps;

        _db.ChangeApprovals.Add(new ChangeApproval
        {
            TicketId = t.Id,
            Step = step,
            ApproverId = me.Id,
            Decision = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected,
            DecidedAt = AppTime.UtcNow,
            Note = note
        });

        string mesaj;
        if (!approve)
        {
            t.ApprovalStatus = ApprovalStatus.Rejected;
            t.Status = TicketStatus.Closed;      // reddedilen talep kapanır
            t.ResolvedAt ??= AppTime.UtcNow;
            t.ClosedAt ??= AppTime.UtcNow;
            mesaj = "reddedildi";
        }
        else if (step >= required)
        {
            t.ApprovalStatus = ApprovalStatus.Approved;
            mesaj = "onaylandı";
        }
        else
        {
            // Zincir sürüyor: talep beklemede kalır, bir sonraki adım yöneticide
            mesaj = $"{step}. adımda onaylandı, {required}. adım bekleniyor";
        }

        t.ApprovalNote = note;
        t.ApprovalDecidedAt = AppTime.UtcNow;

        if (t.ApprovalStatus != ApprovalStatus.Pending)
        {
            _notify.Queue(t.RequesterId, NotificationType.ApprovalDecided,
                $"Talebiniz {(approve ? "onaylandı" : "reddedildi")}: {Ui.TicketNo(t)} {t.Title}",
                string.IsNullOrWhiteSpace(note) ? "Karar verildi." : note!, t.Id);
        }
        else
        {
            await _notify.QueueToRoleAsync(UserRole.Admin, NotificationType.ApprovalPending,
                $"İkinci onay bekliyor: {Ui.TicketNo(t)} {t.Title}",
                $"{me.FullName} ilk onayı verdi; yüksek riskli değişiklik yönetici onayı bekliyor.",
                t.Id, exceptUserId: me.Id);
        }

        await _db.SaveChangesAsync();
        await _notify.DeliverPendingEmailsAsync();

        TempData["Ok"] = $"{Ui.TicketNo(t)} {mesaj}.";
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
    }
}
