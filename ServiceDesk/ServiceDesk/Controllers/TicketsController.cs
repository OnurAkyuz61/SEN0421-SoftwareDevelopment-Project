using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// YÖNETİM: tüm talepleri görme, atama, durum güncelleme
// (Yeni talep açma artık Kullanıcı Portalında: /Portal/Create)
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class TicketsController : Controller
{
    private readonly AppDbContext _db;
    private readonly FileStorage _files;
    private readonly CurrentUser _current;
    private readonly NotificationService _notify;

    public TicketsController(AppDbContext db, FileStorage files, CurrentUser current, NotificationService notify)
    {
        _db = db;
        _files = files;
        _current = current;
        _notify = notify;
    }

    // Filtreler: arama metni, durum, öncelik, tür, kategori
    public async Task<IActionResult> Index(string? q, TicketStatus? status, Priority? priority, TicketType? type, int? categoryId)
    {
        var query = _db.Tickets.Include(t => t.Assignee).Include(t => t.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(t => t.Title.Contains(q) || t.RequesterName.Contains(q)
                || (t.Assignee != null && (t.Assignee.FirstName + " " + t.Assignee.LastName).Contains(q)));
        if (status != null) query = query.Where(t => t.Status == status);
        if (priority != null) query = query.Where(t => t.Priority == priority);
        if (type != null) query = query.Where(t => t.Type == type);
        if (categoryId != null) query = query.Where(t => t.CategoryId == categoryId);

        ViewBag.Q = q; ViewBag.Status = status; ViewBag.Priority = priority; ViewBag.Type = type;
        ViewBag.CategoryId = categoryId;
        ViewBag.Categories = await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();

        // İhlal işaretleme ve eskalasyon artık SlaMonitorService'in işi;
        // bu sayfa yalnızca okur
        var list = await query.AsNoTracking().OrderByDescending(t => t.CreatedAt).ToListAsync();

        return View(list);
    }

    public async Task<IActionResult> Details(int id)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Feedback)
            .Include(t => t.Attachments)
            .Include(t => t.Assignee)
            .Include(t => t.Requester)
            .Include(t => t.Comments).ThenInclude(c => c.Author)
            .Include(t => t.Comments).ThenInclude(c => c.Attachments)
            .Include(t => t.History).ThenInclude(h => h.ChangedBy)
            .Include(t => t.Escalations).ThenInclude(e => e.EscalatedTo)
            .Include(t => t.SlaPolicy)
            .Include(t => t.Category)
            .Include(t => t.AssignedTeam)
            .Include(t => t.ChangeDetail)
            .Include(t => t.Approvals).ThenInclude(a => a.Approver)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();

        ticket.Comments = ticket.Comments.OrderBy(c => c.CreatedAt).ToList();
        ticket.History = ticket.History.OrderByDescending(h => h.ChangedAt).ToList();
        ticket.Escalations = ticket.Escalations.OrderByDescending(e => e.OccurredAt).ToList();

        // Atama listesi: aktif Destek ve Yönetici rolündeki kullanıcılar
        ViewBag.Staff = await _db.Users
            .Where(u => u.Role != UserRole.User && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .ToListAsync();
        return View(ticket);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, TicketStatus status, int? assigneeId, string? rowVersion)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        // Formun açıldığı andaki sürüm: aradan başka biri kaydettiyse yazma reddedilir
        if (!string.IsNullOrEmpty(rowVersion))
            _db.Entry(ticket).Property(t => t.RowVersion).OriginalValue = Convert.FromBase64String(rowVersion);

        // Talep yalnızca aktif destek personeline atanabilir
        if (assigneeId != null)
        {
            var isStaff = await _db.Users.AnyAsync(u =>
                u.Id == assigneeId && u.IsActive && u.Role != UserRole.User);

            if (!isStaff)
            {
                TempData["Err"] = "Seçilen kişi bu talebe atanamaz.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // Onaylanmamış değişiklik talebi üzerinde çalışılamaz (Change Management kuralı)
        if (ticket.Type == TicketType.Change && ticket.ApprovalStatus != ApprovalStatus.Approved
            && status is TicketStatus.InProgress or TicketStatus.Resolved)
        {
            TempData["Err"] = "Bu değişiklik talebi onaylanmadan işleme alınamaz.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var wasFinished = ticket.Status is TicketStatus.Resolved or TicketStatus.Closed;
        var isFinished = status is TicketStatus.Resolved or TicketStatus.Closed;
        var previousAssignee = ticket.AssigneeId;

        ticket.Status = status;
        ticket.AssigneeId = assigneeId;

        // Yeni atanan kişiye bildirim
        if (assigneeId != null && assigneeId != previousAssignee)
        {
            _notify.Queue(assigneeId.Value, NotificationType.Assigned,
                $"Size atandı: #{ticket.Id} {ticket.Title}",
                $"Hedef çözüm: {AppTime.ToLocal(ticket.SlaDueAt):dd.MM.yyyy HH:mm}", ticket.Id);
        }

        if (isFinished)
        {
            ticket.ResolvedAt ??= AppTime.UtcNow;
            if (status == TicketStatus.Closed) ticket.ClosedAt ??= AppTime.UtcNow;
        }
        else if (wasFinished)
        {
            // Yeniden açılış: sayaç artar, önceki çözüm bilgisi geçmişte kalır
            ticket.ReopenCount++;
            ticket.ResolvedAt = null;
            ticket.ClosedAt = null;
        }

        try
        {
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Talep güncellendi.";
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Err"] = "Bu talep siz sayfayı açtıktan sonra başka biri tarafından güncellendi. " +
                              "Aşağıdaki güncel hali inceleyip değişikliğinizi tekrar uygulayın.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // Değişiklik talebinin planını kaydeder. Risk "Yüksek" seçilirse onay
    // zinciri iki adıma çıkar, bu yüzden plan onaydan önce doldurulmalıdır.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateChangeDetail(
        int id, RiskLevel risk, DateTime? plannedStart, DateTime? plannedEnd,
        string? implementationPlan, string? rollbackPlan, string? impactAnalysis)
    {
        var ticket = await _db.Tickets.Include(t => t.ChangeDetail).FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();

        if (ticket.Type != TicketType.Change)
        {
            TempData["Err"] = "Değişiklik planı yalnızca Değişiklik taleplerinde tutulur.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var detail = ticket.ChangeDetail;
        if (detail == null)
        {
            detail = new ChangeDetail { TicketId = ticket.Id };
            _db.ChangeDetails.Add(detail);
        }

        detail.Risk = risk;
        // Form yerel saat gönderir, veritabanı UTC saklar
        detail.PlannedStart = plannedStart == null ? null : AppTime.ToUtc(plannedStart.Value);
        detail.PlannedEnd = plannedEnd == null ? null : AppTime.ToUtc(plannedEnd.Value);
        detail.ImplementationPlan = implementationPlan;
        detail.RollbackPlan = rollbackPlan;
        detail.ImpactAnalysis = impactAnalysis;
        detail.UpdatedAt = AppTime.UtcNow;
        detail.UpdatedById = _current.User!.Id;

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Değişiklik planı kaydedildi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Talebi kalıcı silmek yerine pasifleştirir: kayıt veritabanında kalır,
    // sorgulara düşmez. Yalnızca yönetici yapabilir (ISO 9001 md. 7.5).
    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> SoftDelete(int id, string? reason)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        ticket.IsDeleted = true;
        ticket.DeletedAt = AppTime.UtcNow;
        ticket.DeletedById = _current.User!.Id;

        _db.TicketHistories.Add(new TicketHistory
        {
            TicketId = ticket.Id,
            Field = "Kayıt",
            OldValue = "Aktif",
            NewValue = "Arşivlendi",
            ChangedById = _current.User!.Id,
            ChangedAt = AppTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Ok"] = $"{Ui.TicketNo(ticket)} arşivlendi. Kayıt silinmedi, listelerde görünmüyor.";
        return RedirectToAction(nameof(Index));
    }

    // Destek personelinin yazışması. İç not işaretlenirse kullanıcı göremez.
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> AddComment(int id, string? body, bool isInternal, int? workMinutes, List<IFormFile>? files)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["Err"] = "Mesaj boş olamaz.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var fileError = FileStorage.Validate(files);
        if (fileError != null)
        {
            TempData["Err"] = fileError;
            return RedirectToAction(nameof(Details), new { id });
        }

        var comment = new TicketComment
        {
            TicketId = id,
            AuthorId = _current.User!.Id,
            Body = body.Trim(),
            IsInternal = isInternal,
            WorkMinutes = workMinutes
        };

        foreach (var a in await _files.SaveAsync(files))
        {
            a.TicketId = id;
            a.UploadedById = _current.User!.Id;
            comment.Attachments.Add(a);
        }

        _db.TicketComments.Add(comment);

        // İlk yanıt: kullanıcıya görünen ilk destek mesajı SLA'nın yanıt ayağını kapatır
        if (!isInternal && ticket.FirstRespondedAt == null)
            ticket.FirstRespondedAt = AppTime.UtcNow;

        // Kullanıcıya yazılan yanıt talep sahibine bildirilir; iç not bildirilmez
        if (!isInternal)
            _notify.Queue(ticket.RequesterId, NotificationType.NewComment,
                $"Talebinize yanıt: #{ticket.Id} {ticket.Title}",
                $"{_current.User!.FullName} yanıt yazdı.", ticket.Id);

        await _db.SaveChangesAsync();

        TempData["Ok"] = isInternal ? "İç not eklendi." : "Yanıtınız talebe eklendi.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
