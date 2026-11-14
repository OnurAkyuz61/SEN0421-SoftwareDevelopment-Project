using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Approvals;

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
    private readonly ApprovalChainResolver _chain;
    private readonly ChangeResubmissionService _resubmit;
    private readonly TeamRoundRobin _roundRobin;
    private readonly AttachmentQuota _quota;
    private readonly SurveySettings _survey;

    public PortalController(AppDbContext db, FileStorage files, CurrentUser current,
                            TicketSlaService sla, NotificationService notify,
                            NonconformityService nonconformity, ApprovalChainResolver chain,
                            ChangeResubmissionService resubmit, TeamRoundRobin roundRobin,
                            AttachmentQuota quota, IOptions<SurveySettings> survey)
    {
        _db = db;
        _files = files;
        _current = current;
        _sla = sla;
        _notify = notify;
        _nonconformity = nonconformity;
        _chain = chain;
        _resubmit = resubmit;
        _roundRobin = roundRobin;
        _quota = quota;
        _survey = survey.Value;
    }

    // Reddedilen değişiklik talebini düzeltip yeniden onaya gönderir (talep sahibi)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resubmit(int id, string? correctionNote, string? description,
        DateTime? plannedStart, DateTime? plannedEnd, string? returnTo)
    {
        var ticket = await _db.Tickets.Include(t => t.ChangeDetail).Include(t => t.Approvals)
            .FirstOrDefaultAsync(t => t.Id == id && t.RequesterId == _current.User!.Id);
        if (ticket == null) return NotFound();

        var (ok, message) = await _resubmit.ResubmitAsync(ticket, _current.User!, correctionNote ?? "",
            description, newRisk: null,
            plannedStart == null ? null : AppTime.ToUtc(plannedStart.Value),
            plannedEnd == null ? null : AppTime.ToUtc(plannedEnd.Value));

        TempData[ok ? "Ok" : "Err"] = message;
        return BackToTicket(id, returnTo);
    }

    public async Task<IActionResult> Index()
    {
        var me = _current.User!;

        var list = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Category)!.ThenInclude(c => c!.Parent)
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

    private async Task LoadCategoriesAsync()
    {
        var list = await _db.Categories.AsNoTracking()
            .Include(c => c.Parent)
            .Where(c => c.IsActive)
            .ToListAsync();
        ViewBag.Categories = CatalogRules.OrderByHierarchy(list, c => c.Id, c => c.ParentId,
            c => c.SortOrder, c => c.Name);
    }

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

        // Talep eki sınırı + kullanıcı kotası (yeni talep: ticketId henüz yok)
        var fileError = _files.Validate(files, UploadKind.Ticket)
                        ?? await _quota.CheckAsync(me.Id, ticketId: null, files);
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

        // Ekipte round-robin açıksa talep sıradaki personele otomatik atanır
        var otomatikAtanan = await _roundRobin.AssignAsync(ticket);

        // Değişiklik talepleri yönetici onayına düşer
        ticket.ApprovalStatus = ticket.Type == TicketType.Change ? ApprovalStatus.Pending : ApprovalStatus.NotRequired;

        // Değişiklik talebinin plan kaydı (risk, takvim, geri alma) talep açılırken oluşur;
        // destek ekibi planı talep detayından doldurur
        if (ticket.Type == TicketType.Change)
        {
            ticket.ChangeDetail = new ChangeDetail { UpdatedById = me.Id };
            // Onay adımı sayısı zincir kurallarına göre (kategori / öncelik)
            await _chain.ApplyAsync(ticket);
        }
        ticket.Attachments = await _files.SaveAsync(files);
        foreach (var a in ticket.Attachments) a.UploadedById = me.Id;

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        // Numara Id'den türetilir, bu yüzden ilk kayıttan sonra yazılır
        ticket.Number = Ui.BuildTicketNumber(ticket);
        await _db.SaveChangesAsync();

        if (otomatikAtanan != null)
            _notify.Queue(otomatikAtanan.Value, NotificationType.Assigned,
                $"Size atandı: {Ui.TicketNo(ticket)} {ticket.Title}",
                $"Ekip sırasına göre otomatik atandı. Hedef çözüm: {AppTime.ToLocal(ticket.SlaDueAt):dd.MM.yyyy HH:mm}",
                ticket.Id);

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
        _notify.SignalDelivery();

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
            .Include(t => t.Category)!.ThenInclude(c => c!.Parent)
            .Include(t => t.ChangeDetail)
            .Include(t => t.Approvals)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id && t.RequesterId == _current.User!.Id);

        if (ticket == null) return null;

        ViewBag.CanResubmit = ApprovalPolicy.CanResubmit(ticket, _current.User!, _resubmit.MaxResubmissions);

        ViewBag.SurveyQuestions = await _db.SurveyQuestions
            .AsNoTracking()
            .Where(q => q.IsActive && q.Template!.IsActive)
            .OrderBy(q => q.SortOrder)
            .ToListAsync();

        // Anket penceresi: form mu gösterilecek, "süre doldu" bilgisi mi.
        // Kural tek yerde (SurveyPolicy); görüntü yalnızca sonucu okur.
        ViewBag.SurveyDeadline = SurveyPolicy.Deadline(ticket, _survey.ExpiresAfterDays);
        ViewBag.SurveyExpired = SurveyPolicy.IsExpired(ticket, AppTime.UtcNow, _survey.ExpiresAfterDays);

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

        // Yorum eki sınırı (talep ekinden ayrı) + talep ve kullanıcı kotası
        var fileError = _files.Validate(files, UploadKind.Comment)
                        ?? await _quota.CheckAsync(_current.User!.Id, id, files);
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

        // Destek kullanıcıdan bilgi bekliyorduysa yanıtla birlikte talep yeniden
        // işleme alınır ve SLA sayacı kaldığı yerden devam eder
        if (ticket.Status == TicketStatus.WaitingForCustomer)
            await _sla.ApplyStatusAsync(ticket, TicketStatus.InProgress, AppTime.UtcNow);

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

    // Çözülen talebi kullanıcı yeniden açar (sorun devam ediyorsa). Neden zorunlu,
    // yazışmaya eklenir; yeniden açılma sayacı TicketWorkflow'da artar.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int id, string? reason, string? returnTo)
    {
        var ticket = await FindMyTicketAsync(id);
        if (ticket == null) return NotFound();

        reason = reason?.Trim();
        string? hata =
            ticket.Status != TicketStatus.Resolved ? "Yalnızca çözülmüş talepler yeniden açılabilir."
            : string.IsNullOrEmpty(reason) || reason.Length < 5 ? "Yeniden açma nedenini yazın (en az 5 karakter)."
            : reason.Length > 1000 ? "Neden en fazla 1000 karakter olabilir."
            : TicketWorkflow.CanTransition(ticket, TicketStatus.InProgress) is { Allowed: false } k ? k.Error
            : null;
        if (hata != null)
        {
            TempData["Err"] = hata;
            return BackToTicket(id, returnTo);
        }

        await _sla.ApplyStatusAsync(ticket, TicketStatus.InProgress, AppTime.UtcNow);

        _db.TicketComments.Add(new TicketComment
        {
            TicketId = id, AuthorId = _current.User!.Id, IsInternal = false,
            Body = $"Talep kullanıcı tarafından yeniden açıldı. Neden: {reason}"
        });

        await NotifyOwnersAsync(ticket, $"Talep yeniden açıldı: {Ui.TicketNo(ticket)} {ticket.Title}",
            $"{_current.User!.FullName}: {reason}");

        await _db.SaveChangesAsync();
        _notify.SignalDelivery();

        TempData["Ok"] = "Talebiniz yeniden açıldı; destek ekibi tekrar ilgilenecek.";
        return BackToTicket(id, returnTo);
    }

    // Kullanıcı açık talebinin iptalini ister; destek onaylarsa talep kapanır
    // (TicketsController.DecideCancel)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestCancel(int id, string? reason, string? returnTo)
    {
        var ticket = await FindMyTicketAsync(id);
        if (ticket == null) return NotFound();

        reason = reason?.Trim();
        string? hata =
            !TicketWorkflow.IsOpen(ticket.Status) ? "Yalnızca açık talepler için iptal istenebilir."
            : ticket.CancelRequestedAt != null ? "Bu talep için zaten bekleyen bir iptal isteği var."
            : string.IsNullOrEmpty(reason) ? "İptal nedenini yazın."
            : reason.Length > 500 ? "Neden en fazla 500 karakter olabilir."
            : null;
        if (hata != null)
        {
            TempData["Err"] = hata;
            return BackToTicket(id, returnTo);
        }

        ticket.CancelRequestedAt = AppTime.UtcNow;
        ticket.CancelReason = reason;

        _db.TicketHistories.Add(new TicketHistory
        {
            TicketId = id, Field = "İptal isteği", OldValue = "-", NewValue = "Bekliyor",
            ChangedById = _current.User!.Id, ChangedAt = AppTime.UtcNow
        });

        await NotifyOwnersAsync(ticket, $"İptal isteği: {Ui.TicketNo(ticket)} {ticket.Title}",
            $"{_current.User!.FullName} talebin iptalini istedi: {reason}");

        await _db.SaveChangesAsync();
        _notify.SignalDelivery();

        TempData["Ok"] = "İptal isteğiniz destek ekibine iletildi.";
        return BackToTicket(id, returnTo);
    }

    // İlgilenen varsa ona, yoksa destek ekibine
    private async Task NotifyOwnersAsync(Ticket ticket, string title, string message)
    {
        if (ticket.AssigneeId != null)
            _notify.Queue(ticket.AssigneeId.Value, NotificationType.NewComment, title, message, ticket.Id);
        else
            await _notify.QueueToRoleAsync(UserRole.Agent, NotificationType.NewComment, title, message, ticket.Id);
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

        var err = _files.Validate(files, UploadKind.Ticket)
                  ?? await _quota.CheckAsync(_current.User!.Id, id, files);
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

        // Doldurma süresi (TTL) doldu mu? Form ekranda artık görünmüyor ama
        // açık duran bir sekmeden ya da doğrudan POST ile gelinebilir:
        // asıl engel burada. Aylar sonra girilen puan dönemsel memnuniyet
        // ortalamasını bozar (ISO 9001 md. 9.1.2).
        if (SurveyPolicy.IsExpired(ticket, AppTime.UtcNow, _survey.ExpiresAfterDays))
        {
            var deadline = SurveyPolicy.Deadline(ticket, _survey.ExpiresAfterDays)!.Value;
            TempData["Err"] = $"Bu talebin değerlendirme süresi {AppTime.ToLocal(deadline):dd.MM.yyyy} "
                            + "tarihinde doldu. Görüşünüzü iletmek için yeni bir talep açabilirsiniz.";
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

        // Soru bazlı kırılım: uygunsuzluk açıklamasında hangi boyutun düşük
        // puan aldığı yazılır (NonconformityPolicy)
        var puanlar = new List<NonconformityPolicy.AnswerScore>();

        // Tüm aktif sorular zorunlu: biri bile boş veya geçersizse anket kaydedilmez
        foreach (var q in template.Questions.OrderBy(q => q.SortOrder))
        {
            if (!int.TryParse(Request.Form[$"q{q.Id}"], out var score) || score is < 1 or > 5)
            {
                TempData["Err"] = "Lütfen anketteki tüm soruları puanlayın.";
                return BackToTicket(id, returnTo, survey: true);
            }
            response.Answers.Add(new SurveyAnswer { QuestionId = q.Id, Score = score });
            puanlar.Add(new NonconformityPolicy.AnswerScore(q.Text, score));
        }

        if (response.Answers.Count == 0)
        {
            TempData["Err"] = "Ankette puanlanacak soru bulunamadı.";
            return BackToTicket(id, returnTo, survey: true);
        }

        _db.SurveyResponses.Add(response);

        // Eşik altı memnuniyet kalite kaydı doğurur (ISO 9001 md. 10.2).
        // Gerekçe iki tanedir: ortalamanın eşik altı olması ya da tek bir
        // sorunun kritik düşük puanı (NonconformityPolicy).
        var ortalama = response.Answers.Average(a => a.Score);
        var acildi = await _nonconformity.QueueForLowSatisfactionAsync(ticket, ortalama, puanlar, comment);

        await _db.SaveChangesAsync();

        // Kayıt açıldıysa sorumluya bildirim kuyruğa girdi: worker'ı uyandır
        if (acildi) _notify.SignalDelivery();

        TempData["Ok"] = acildi
            ? "Değerlendirmeniz alındı. Memnuniyetsizliğiniz kalite kaydı olarak açıldı, ekibimiz inceleyecek."
            : "Değerlendirmeniz için teşekkürler.";
        return BackToTicket(id, returnTo);
    }
}
