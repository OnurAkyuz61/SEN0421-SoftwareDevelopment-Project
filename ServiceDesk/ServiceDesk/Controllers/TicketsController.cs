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

    public TicketsController(AppDbContext db, FileStorage files, CurrentUser current)
    {
        _db = db;
        _files = files;
        _current = current;
    }

    // Filtreler: arama metni, durum, öncelik, tür, kategori
    public async Task<IActionResult> Index(string? q, TicketStatus? status, Priority? priority, TicketType? type, TicketCategory? category)
    {
        var query = _db.Tickets.Include(t => t.Assignee).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(t => t.Title.Contains(q) || t.RequesterName.Contains(q)
                || (t.Assignee != null && (t.Assignee.FirstName + " " + t.Assignee.LastName).Contains(q)));
        if (status != null) query = query.Where(t => t.Status == status);
        if (priority != null) query = query.Where(t => t.Priority == priority);
        if (type != null) query = query.Where(t => t.Type == type);
        if (category != null) query = query.Where(t => t.Category == category);

        ViewBag.Q = q; ViewBag.Status = status; ViewBag.Priority = priority; ViewBag.Type = type; ViewBag.Category = category;

        var list = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

        // Basit eskalasyon: SLA aşılan açık talepler işaretlenir
        // (Faz 3'te bu iş arka plan servisine taşınacak; GET içinde yazmak geçici)
        foreach (var t in list.Where(t => t.IsSlaBreached && t.ResolvedAt == null && !t.IsEscalated))
            t.IsEscalated = true;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Aynı anda listeyi açan başka bir kullanıcı işaretlemiş olabilir; liste yine doğru görünür
        }

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
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();

        ticket.Comments = ticket.Comments.OrderBy(c => c.CreatedAt).ToList();
        ticket.History = ticket.History.OrderByDescending(h => h.ChangedAt).ToList();

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

        ticket.Status = status;
        ticket.AssigneeId = assigneeId;

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

        // İlk yanıt zamanı: SLA'nın yanıt ayağı Faz 3'te bu alandan hesaplanacak
        await _db.SaveChangesAsync();

        TempData["Ok"] = isInternal ? "İç not eklendi." : "Yanıtınız talebe eklendi.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
