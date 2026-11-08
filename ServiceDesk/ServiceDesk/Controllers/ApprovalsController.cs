using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Approvals;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Controllers;

// Bekleyen onay listesinin filtreleri
public class ApprovalFilter
{
    public string? Q { get; set; }
    public RiskLevel? Risk { get; set; }
    public Priority? Priority { get; set; }
    public int? CategoryId { get; set; }
    public bool? Resubmitted { get; set; }
    public bool? WindowPassed { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
}

// YÖNETİM: Değişiklik taleplerinin onay / red işlemleri ve vekalet
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class ApprovalsController : Controller
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notify;
    private readonly CurrentUser _current;
    private readonly ApprovalAuthority _authority;
    private readonly AuditService _audit;

    public ApprovalsController(AppDbContext db, NotificationService notify, CurrentUser current,
                               ApprovalAuthority authority, AuditService audit)
    {
        _db = db;
        _notify = notify;
        _current = current;
        _authority = authority;
        _audit = audit;
    }

    // Bekleyen onaylar: filtreli ve sayfalı (filtreleme ve sayfalama SQL'de)
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> Index([FromQuery] ApprovalFilter f)
    {
        var now = AppTime.UtcNow;
        var q = _db.Tickets.AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.ChangeDetail)
            .Include(t => t.Approvals).ThenInclude(a => a.Approver)
            .Include(t => t.Approvals).ThenInclude(a => a.OnBehalfOf)
            .Where(t => t.ApprovalStatus == ApprovalStatus.Pending);

        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var s = f.Q.Trim();
            q = q.Where(t => t.Title.Contains(s) || t.RequesterName.Contains(s) || (t.Number != null && t.Number.Contains(s)));
        }
        if (f.Risk != null) q = q.Where(t => (t.ChangeDetail != null ? t.ChangeDetail.Risk : RiskLevel.Low) == f.Risk);
        if (f.Priority != null) q = q.Where(t => t.Priority == f.Priority);
        if (f.CategoryId != null) q = q.Where(t => t.CategoryId == f.CategoryId);
        if (f.Resubmitted == true) q = q.Where(t => t.ApprovalRound > 1);
        if (f.WindowPassed == true) q = q.Where(t => t.ChangeDetail != null && t.ChangeDetail.PlannedEnd != null && t.ChangeDetail.PlannedEnd < now);
        if (f.From is { } fromLocal) { var u = AppTime.ToUtc(fromLocal.Date); q = q.Where(t => t.CreatedAt >= u); }
        if (f.To is { } toLocal) { var u = AppTime.ToUtc(toLocal.Date.AddDays(1)); q = q.Where(t => t.CreatedAt < u); }

        q = f.Sort switch
        {
            "newest" => q.OrderByDescending(t => t.CreatedAt),
            "planned" => q.OrderBy(t => t.ChangeDetail!.PlannedStart == null).ThenBy(t => t.ChangeDetail!.PlannedStart),
            "priority" => q.OrderByDescending(t => t.Priority).ThenBy(t => t.CreatedAt),
            _ => q.OrderBy(t => t.CreatedAt)
        };

        var (items, pager) = await q.AsSplitQuery().ToPageAsync(f.Page);

        ViewBag.Filter = f;
        ViewBag.Pager = pager;
        ViewBag.Context = await _authority.ContextForAsync(_current.User!);
        ViewBag.Categories = await _db.Categories.AsNoTracking().Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();
        ViewBag.History = await _db.Tickets.AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.ApprovalStatus == ApprovalStatus.Approved || t.ApprovalStatus == ApprovalStatus.Rejected)
            .OrderByDescending(t => t.ApprovalDecidedAt).Take(10).ToListAsync();

        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(int id, string? note, string? returnUrl, string? rowVersion) =>
        Decide(id, true, note, returnUrl, rowVersion);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(int id, string? note, string? returnUrl, string? rowVersion) =>
        Decide(id, false, note, returnUrl, rowVersion);

    private async Task<IActionResult> Decide(int id, bool approve, string? note, string? returnUrl, string? rowVersion)
    {
        var t = await _db.Tickets
            .Include(x => x.ChangeDetail)
            .Include(x => x.Approvals)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();

        if (TicketConcurrency.Expect(_db, t, rowVersion)) return await ConflictAsync(id);

        var me = _current.User!;

        // Yetki kuralları tek yerde: ApprovalPolicy (rol, kendi talebi, nihai
        // karar, vekalet, uygulama penceresi, CAB kaydı, zincirde tekrar onay)
        var yetki = ApprovalPolicy.CanDecide(t, me, approve, await _authority.ContextForAsync(me));
        if (!yetki.Allowed)
        {
            TempData["Err"] = yetki.Error;
            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
        }

        if (!approve && string.IsNullOrWhiteSpace(note))
        {
            TempData["Err"] = "Red gerekçesi zorunludur: talep sahibi düzeltip yeniden gönderebilmeli.";
            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
        }

        var step = ApprovalPolicy.NextStep(t);
        var required = t.RequiredApprovalSteps;
        var vekalet = yetki.OnBehalfOf;

        _db.ChangeApprovals.Add(new ChangeApproval
        {
            TicketId = t.Id,
            Round = t.ApprovalRound,
            Step = step,
            ApproverId = me.Id,
            OnBehalfOfId = vekalet?.Id,
            Decision = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected,
            DecidedAt = AppTime.UtcNow,
            Note = note
        });

        string mesaj;
        if (!approve)
        {
            t.ApprovalStatus = ApprovalStatus.Rejected;
            t.Status = TicketStatus.Closed;      // reddedilen talep kapanır (düzeltilip yeniden gönderilebilir)
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
            mesaj = $"{step}. adımda onaylandı, {step + 1}/{required}. adım bekleniyor";
        }

        t.ApprovalNote = note;
        t.ApprovalDecidedAt = AppTime.UtcNow;
        var vekaletNotu = vekalet == null ? "" : $" ({vekalet.FullName} adına vekaleten)";

        if (t.ApprovalStatus != ApprovalStatus.Pending)
        {
            _notify.Queue(t.RequesterId, NotificationType.ApprovalDecided,
                $"Talebiniz {(approve ? "onaylandı" : "reddedildi")}: {Ui.TicketNo(t)} {t.Title}",
                (string.IsNullOrWhiteSpace(note) ? "Karar verildi." : note!) + vekaletNotu
                    + (approve ? "" : " Talebi düzeltip yeniden onaya gönderebilirsiniz."),
                t.Id, EmailTemplates.ApprovalDecided);
        }
        else
        {
            var nextFinal = step + 1 >= required;
            var role = nextFinal ? UserRole.Admin : UserRole.Agent;
            await _notify.QueueToRoleAsync(role, NotificationType.ApprovalPending,
                $"{step + 1}. onay bekliyor: {Ui.TicketNo(t)} {t.Title}",
                $"{me.FullName}{vekaletNotu} {step}. onayı verdi; değişiklik {step + 1}/{required}. adımı bekliyor.",
                t.Id, exceptUserId: me.Id, templateKey: EmailTemplates.ApprovalPending);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException ||
                                   ex is DbUpdateException u && AppDbContext.IsUniqueViolation(u))
        {
            TicketConcurrency.DiscardChanges(_db);
            return await ConflictAsync(id);
        }
        _notify.SignalDelivery();

        TempData["Ok"] = $"{Ui.TicketNo(t)} {mesaj}{vekaletNotu}.";
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    // ───── Vekalet ───────────────────────────────────────────────────────

    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Delegations()
    {
        var me = _current.User!;
        ViewBag.Staff = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Role != UserRole.User && u.Id != me.Id)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();
        ViewBag.Received = await _db.ApprovalDelegations.AsNoTracking()
            .Include(d => d.Delegator)
            .Where(d => d.DelegateId == me.Id && d.RevokedAt == null && d.EndsAt > AppTime.UtcNow)
            .ToListAsync();

        return View(await _db.ApprovalDelegations.AsNoTracking()
            .Include(d => d.Delegate)
            .Where(d => d.DelegatorId == me.Id)
            .OrderByDescending(d => d.CreatedAt).Take(50).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Delegate(int delegateId, DateTime startsAt, DateTime endsAt, string? reason)
    {
        var me = _current.User!;
        var startUtc = AppTime.ToUtc(startsAt);
        var endUtc = AppTime.ToUtc(endsAt);
        var max = _authority.Settings.MaxDelegationDays;

        string? err = null;
        if (delegateId == me.Id) err = "Kendinize vekalet veremezsiniz.";
        else if (endUtc <= startUtc) err = "Bitiş, başlangıçtan sonra olmalı.";
        else if (endUtc <= AppTime.UtcNow) err = "Bitiş geçmişte olamaz.";
        else if ((endUtc - startUtc).TotalDays > max) err = $"Vekalet en fazla {max} gün olabilir.";
        else if (!await _db.Users.AnyAsync(u => u.Id == delegateId && u.IsActive && u.Role != UserRole.User))
            err = "Vekil yalnızca aktif destek veya yönetici olabilir.";
        else if (await _db.ApprovalDelegations.AnyAsync(d => d.DelegatorId == me.Id && d.DelegateId == delegateId
                     && d.RevokedAt == null && d.StartsAt < endUtc && startUtc < d.EndsAt))
            err = "Bu kişiye aynı dönem için zaten vekalet verilmiş.";

        if (err != null)
        {
            TempData["Err"] = err;
            return RedirectToAction(nameof(Delegations));
        }

        var d = new ApprovalDelegation
        {
            DelegatorId = me.Id, DelegateId = delegateId,
            StartsAt = startUtc, EndsAt = endUtc,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(300, reason.Trim().Length)]
        };
        _db.ApprovalDelegations.Add(d);
        _audit.Queue(AuditAction.DelegationCreated, me.Id,
            detail: $"Vekil #{delegateId}, {startsAt:dd.MM.yyyy HH:mm} – {endsAt:dd.MM.yyyy HH:mm}. {reason}");
        _notify.Queue(delegateId, NotificationType.ApprovalPending,
            "Onay yetkisi size devredildi",
            $"{me.FullName}, {startsAt:dd.MM.yyyy HH:mm} – {endsAt:dd.MM.yyyy HH:mm} arasında değişiklik onay yetkisini size devretti.",
            templateKey: EmailTemplates.ApprovalPending);

        await _db.SaveChangesAsync();
        _notify.SignalDelivery();
        TempData["Ok"] = "Vekalet kaydedildi.";
        return RedirectToAction(nameof(Delegations));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Revoke(int id)
    {
        var me = _current.User!;
        var d = await _db.ApprovalDelegations.FirstOrDefaultAsync(x => x.Id == id && x.DelegatorId == me.Id);
        if (d == null) return NotFound();

        if (d.RevokedAt == null)
        {
            d.RevokedAt = AppTime.UtcNow;
            _audit.Queue(AuditAction.DelegationRevoked, me.Id, detail: $"Vekalet #{d.Id} (vekil #{d.DelegateId}) kaldırıldı");
            await _db.SaveChangesAsync();
        }

        TempData["Ok"] = "Vekalet kaldırıldı.";
        return RedirectToAction(nameof(Delegations));
    }

    private async Task<IActionResult> ConflictAsync(int id)
    {
        TempData["Err"] = await TicketConcurrency.ConflictMessageAsync(_db, id, _current.User?.Id);
        return RedirectToAction("Details", "Tickets", new { id });
    }
}
