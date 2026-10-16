using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// KULLANICI PORTALI: talep açma, dosya yükleme, kendi taleplerini takip.
// Liste ve detay yalnızca oturumdaki kullanıcının kendi taleplerini gösterir;
// tüm talepleri görmek yönetim tarafının işidir (/Tickets).
[AuthorizeRole]
public class PortalController : Controller
{
    private readonly AppDbContext _db;
    private readonly FileStorage _files;
    private readonly CurrentUser _current;
    private readonly TicketSlaService _sla;
    private readonly NotificationService _notify;

    public PortalController(AppDbContext db, FileStorage files, CurrentUser current,
                            TicketSlaService sla, NotificationService notify)
    {
        _db = db;
        _files = files;
        _current = current;
        _sla = sla;
        _notify = notify;
    }

    public async Task<IActionResult> Index()
    {
        var me = _current.User!;

        var list = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.RequesterId == me.Id)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return View(list);
    }

    public async Task<IActionResult> Create()
    {
        await LoadCategoriesAsync();
        return View(new Ticket { Type = TicketType.Incident });
    }

    private async Task LoadCategoriesAsync() =>
        ViewBag.Categories = await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync();

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> Create(
        [Bind("Title,Description,Type,Priority,CategoryId")] Ticket ticket,
        List<IFormFile>? files)
    {
        var me = _current.User!;

        // Talep eden formdan gelmez, oturumdan yazılır
        ticket.RequesterId = me.Id;
        ticket.RequesterName = me.FullName;
        ModelState.Remove(nameof(Ticket.RequesterName));

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == ticket.CategoryId && c.IsActive);
        if (category == null) ModelState.AddModelError(nameof(Ticket.CategoryId), "Geçerli bir kategori seçin.");

        var fileError = FileStorage.Validate(files);
        if (fileError != null) ModelState.AddModelError("files", fileError);

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync();
            return View(ticket);
        }

        ticket.CreatedAt = AppTime.UtcNow;
        ticket.Status = TicketStatus.New;
        // Kategorinin varsayılan ekibi varsa talep doğrudan o ekibe düşer
        ticket.AssignedTeamId = category!.DefaultTeamId;
        await _sla.ApplyPolicyAsync(ticket);

        // Değişiklik talepleri yönetici onayına düşer
        ticket.ApprovalStatus = ticket.Type == TicketType.Change ? ApprovalStatus.Pending : ApprovalStatus.NotRequired;
        ticket.Attachments = await _files.SaveAsync(files);
        foreach (var a in ticket.Attachments) a.UploadedById = me.Id;

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        // Değişiklik talebi onay bekliyor: yöneticiler haberdar edilir
        if (ticket.ApprovalStatus == ApprovalStatus.Pending)
        {
            await _notify.QueueToRoleAsync(UserRole.Admin, NotificationType.ApprovalPending,
                $"Onay bekliyor: #{ticket.Id} {ticket.Title}",
                $"{me.FullName} bir değişiklik talep etti.", ticket.Id);
        }
        else
        {
            await _notify.QueueToRoleAsync(UserRole.Agent, NotificationType.NewComment,
                $"Yeni talep: #{ticket.Id} {ticket.Title}",
                $"{me.FullName} tarafından açıldı.", ticket.Id);
        }
        await _db.SaveChangesAsync();

        TempData["Ok"] = $"Talebiniz alındı. Talep numaranız: #{ticket.Id}";
        return RedirectToAction(nameof(Details), new { id = ticket.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Feedback)
            .Include(t => t.Attachments)
            .Include(t => t.Assignee)
            .Include(t => t.Comments).ThenInclude(c => c.Author)
            .Include(t => t.Comments).ThenInclude(c => c.Attachments)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id && t.RequesterId == _current.User!.Id);

        if (ticket == null) return NotFound();

        // İç notlar kullanıcıya gösterilmez
        ticket.Comments = ticket.Comments
            .Where(c => !c.IsInternal)
            .OrderBy(c => c.CreatedAt)
            .ToList();

        return View(ticket);
    }

    // Kullanıcının kendi talebine yazdığı yanıt
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> AddComment(int id, string? body, List<IFormFile>? files)
    {
        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.Id == id && t.RequesterId == _current.User!.Id);
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
            IsInternal = false   // kullanıcı iç not yazamaz
        };

        foreach (var a in await _files.SaveAsync(files))
        {
            a.TicketId = id;
            a.UploadedById = _current.User!.Id;
            comment.Attachments.Add(a);
        }

        _db.TicketComments.Add(comment);

        // İlgilenen varsa ona, yoksa destek ekibine haber ver
        if (ticket.AssigneeId != null)
            _notify.Queue(ticket.AssigneeId.Value, NotificationType.NewComment,
                $"Yeni mesaj: #{ticket.Id} {ticket.Title}",
                $"{_current.User!.FullName} talebe mesaj yazdı.", ticket.Id);
        else
            await _notify.QueueToRoleAsync(UserRole.Agent, NotificationType.NewComment,
                $"Yeni mesaj: #{ticket.Id} {ticket.Title}",
                $"{_current.User!.FullName} talebe mesaj yazdı.", ticket.Id);

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Mesajınız iletildi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Açılmış bir talebe sonradan dosya / ekran görüntüsü eklemek için
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> AddFiles(int id, List<IFormFile>? files)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id && t.RequesterId == _current.User!.Id);
        if (ticket == null) return NotFound();

        var err = FileStorage.Validate(files);
        if (err != null) TempData["Err"] = err;
        else if (files is { Count: > 0 })
        {
            var saved = await _files.SaveAsync(files);
            foreach (var a in saved) a.UploadedById = _current.User!.Id;
            ticket.Attachments.AddRange(saved);
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Dosyalar eklendi.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Feedback(int id, int score, string? comment)
    {
        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.Id == id && t.RequesterId == _current.User!.Id);
        if (ticket == null) return NotFound();

        var exists = await _db.Feedbacks.AnyAsync(f => f.TicketId == id);
        if (!exists && score is >= 1 and <= 5
            && ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            _db.Feedbacks.Add(new Feedback { TicketId = id, Score = score, Comment = comment });
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Değerlendirmeniz için teşekkürler.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
