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
    private readonly NonconformityService _nonconformity;

    public PortalController(AppDbContext db, FileStorage files, CurrentUser current,
                            TicketSlaService sla, NotificationService notify,
                            NonconformityService nonconformity)
    {
        _db = db;
        _files = files;
        _current = current;
        _sla = sla;
        _notify = notify;
        _nonconformity = nonconformity;
    }

    public async Task<IActionResult> Index()
    {
        var me = _current.User!;

        var list = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Survey)
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

        var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == ticket.CategoryId && c.IsActive);
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

        // Değişiklik talebinin plan kaydı (risk, takvim, geri alma) talep açılırken oluşur;
        // destek ekibi planı talep detayından doldurur
        if (ticket.Type == TicketType.Change)
            ticket.ChangeDetail = new ChangeDetail { UpdatedById = me.Id };
        ticket.Attachments = await _files.SaveAsync(files);
        foreach (var a in ticket.Attachments) a.UploadedById = me.Id;

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        // Numara Id'den türetilir, bu yüzden ilk kayıttan sonra yazılır
        ticket.Number = Ui.BuildTicketNumber(ticket);
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
            await _notify.QueueToRoleAsync(UserRole.Agent, NotificationType.NewTicket,
                $"Yeni talep: {Ui.TicketNo(ticket)} {ticket.Title}",
                $"{me.FullName} tarafından açıldı.", ticket.Id);
        }
        await _db.SaveChangesAsync();
        await _notify.DeliverPendingEmailsAsync();

        TempData["Ok"] = $"Talebiniz alındı. Talep numaranız: {Ui.TicketNo(ticket)}";
        return RedirectToAction(nameof(Details), new { id = ticket.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var ticket = await LoadPortalTicketAsync(id);
        if (ticket == null) return NotFound();
        return View(ticket);
    }

    // Liste üzerinden popup ile açılır; tam sayfa yerine HTML parçası döner
    public async Task<IActionResult> DetailsPartial(int id)
    {
        var ticket = await LoadPortalTicketAsync(id);
        if (ticket == null) return NotFound();
        return PartialView("_DetailsPartial", ticket);
    }

    // Portaldaki her okuma ve yazma bu iki kontrolden geçer:
    //   1) Talep oturumdaki kullanıcıya mı ait (RequesterId == me.Id)
    //   2) Talep arşivlenmiş mi (global query filter: IsDeleted)
    // Başkasının talebi "bulunamadı" sayılır; var olup olmadığı sızdırılmaz,
    // bu yüzden 403 değil 404 dönüyoruz.
    private Task<Ticket?> FindMyTicketAsync(int id) =>
        _db.Tickets.FirstOrDefaultAsync(t => t.Id == id && t.RequesterId == _current.User!.Id);

    private async Task<Ticket?> LoadPortalTicketAsync(int id)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Survey)!.ThenInclude(r => r!.Answers).ThenInclude(a => a.Question)
            .Include(t => t.Attachments)
            .Include(t => t.Assignee)
            .Include(t => t.Comments).ThenInclude(c => c.Author)
            .Include(t => t.Comments).ThenInclude(c => c.Attachments)
            .Include(t => t.Category)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id && t.RequesterId == _current.User!.Id);

        if (ticket == null) return null;

        ViewBag.SurveyQuestions = await _db.SurveyQuestions
            .AsNoTracking()
            .Where(q => q.IsActive && q.Template!.IsActive)
            .OrderBy(q => q.SortOrder)
            .ToListAsync();

        // İç notlar kullanıcıya gösterilmez. Not gizlenirken ekinin talebin
        // "Ekler" listesinde görünmesi dosya adını sızdırıyordu; ek de
        // listeden çıkarılır (indirme yolu AttachmentPolicy ile ayrıca kapalı).
        var icNotIdleri = ticket.Comments.Where(c => c.IsInternal).Select(c => c.Id).ToHashSet();

        ticket.Attachments = ticket.Attachments
            .Where(a => a.CommentId == null || !icNotIdleri.Contains(a.CommentId.Value))
            .ToList();

        ticket.Comments = ticket.Comments
            .Where(c => !c.IsInternal)
            .OrderBy(c => c.CreatedAt)
            .ToList();

        return ticket;
    }

    // Modal içinden gelen formlar listeye dönüp popup'ı yeniden açar
    private IActionResult BackToTicket(int id, string? returnTo, bool survey = false)
    {
        if (string.Equals(returnTo, "modal", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction(nameof(Index), new { open = id, survey = survey ? 1 : (int?)null });
        return RedirectToAction(nameof(Details), new { id });
    }

    // Kullanıcının kendi talebine yazdığı yanıt
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> AddComment(int id, string? body, List<IFormFile>? files, string? returnTo)
    {
        var ticket = await FindMyTicketAsync(id);
        if (ticket == null) return NotFound();

        // Kapanmış talebin yazışması kapalıdır (aynı kural yönetim tarafında da
        // uygulanıyor: TicketsController.AddComment)
        if (ticket.Status == TicketStatus.Closed)
        {
            TempData["Err"] = "Kapanmış talebe mesaj yazılamaz. Yeni bir talep açabilirsiniz.";
            return BackToTicket(id, returnTo);
        }

        if (string.IsNullOrWhiteSpace(body) || body.Length > 4000)
        {
            TempData["Err"] = "Mesaj boş olamaz ve en fazla 4000 karakter olabilir.";
            return BackToTicket(id, returnTo);
        }

        var fileError = FileStorage.Validate(files);
        if (fileError != null)
        {
            TempData["Err"] = fileError;
            return BackToTicket(id, returnTo);
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
                $"Yeni mesaj: {Ui.TicketNo(ticket)} {ticket.Title}",
                $"{_current.User!.FullName} talebe mesaj yazdı.", ticket.Id);
        else
            await _notify.QueueToRoleAsync(UserRole.Agent, NotificationType.NewComment,
                $"Yeni mesaj: {Ui.TicketNo(ticket)} {ticket.Title}",
                $"{_current.User!.FullName} talebe mesaj yazdı.", ticket.Id);

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Mesajınız iletildi.";
        return BackToTicket(id, returnTo);
    }

    // Açılmış bir talebe sonradan dosya / ekran görüntüsü eklemek için
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> AddFiles(int id, List<IFormFile>? files, string? returnTo)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id && t.RequesterId == _current.User!.Id);
        if (ticket == null) return NotFound();   // başkasının talebi: bulunamadı

        if (ticket.Status == TicketStatus.Closed)
        {
            TempData["Err"] = "Kapanmış talebe dosya yüklenemez.";
            return BackToTicket(id, returnTo);
        }

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
        return BackToTicket(id, returnTo);
    }

    // Çok soruluk memnuniyet anketi. Puanlar form üzerinde "q{soruId}" adıyla gelir.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Feedback(int id, string? comment, string? returnTo)
    {
        var ticket = await FindMyTicketAsync(id);
        if (ticket == null) return NotFound();

        if (ticket.Status is not (TicketStatus.Resolved or TicketStatus.Closed))
        {
            TempData["Err"] = "Talep kapanmadan değerlendirme yapılamaz.";
            return BackToTicket(id, returnTo);
        }

        if (await _db.SurveyResponses.AnyAsync(r => r.TicketId == id))
        {
            TempData["Err"] = "Bu talebi zaten değerlendirdiniz.";
            return BackToTicket(id, returnTo);
        }

        var template = await _db.SurveyTemplates
            .Include(t => t.Questions.Where(q => q.IsActive))
            .FirstOrDefaultAsync(t => t.IsActive);
        if (template == null) return NotFound();

        var response = new SurveyResponse
        {
            TicketId = id,
            TemplateId = template.Id,
            SubmittedById = _current.User!.Id,
            Comment = comment
        };

        // Tüm aktif sorular zorunlu: biri bile boş veya geçersizse anket kaydedilmez
        foreach (var q in template.Questions)
        {
            if (!int.TryParse(Request.Form[$"q{q.Id}"], out var score) || score is < 1 or > 5)
            {
                TempData["Err"] = "Lütfen anketteki tüm soruları puanlayın.";
                return BackToTicket(id, returnTo, survey: true);
            }
            response.Answers.Add(new SurveyAnswer { QuestionId = q.Id, Score = score });
        }

        if (response.Answers.Count == 0)
        {
            TempData["Err"] = "Ankette puanlanacak soru bulunamadı.";
            return BackToTicket(id, returnTo, survey: true);
        }

        _db.SurveyResponses.Add(response);

        // Eşik altı memnuniyet kalite kaydı doğurur (ISO 9001 md. 10.2)
        var ortalama = response.Answers.Average(a => a.Score);
        var acildi = await _nonconformity.QueueForLowSatisfactionAsync(ticket, ortalama, comment);

        await _db.SaveChangesAsync();

        TempData["Ok"] = acildi
            ? "Değerlendirmeniz alındı. Memnuniyetsizliğiniz kalite kaydı olarak açıldı, ekibimiz inceleyecek."
            : "Değerlendirmeniz için teşekkürler.";
        return BackToTicket(id, returnTo);
    }
}
