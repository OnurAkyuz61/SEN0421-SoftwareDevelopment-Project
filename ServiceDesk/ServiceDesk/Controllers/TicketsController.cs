using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Approvals;
using ServiceDesk.Services.Escalations;
using ServiceDesk.Services.Notifications;

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
    private readonly EscalationService _escalation;
    private readonly ApprovalAuthority _authority;
    private readonly ApprovalChainResolver _chain;
    private readonly ChangeResubmissionService _resubmit;
    private readonly AuditService _audit;
    private readonly TicketSlaService _sla;

    public TicketsController(AppDbContext db, FileStorage files, CurrentUser current, NotificationService notify,
                             EscalationService escalation, ApprovalAuthority authority,
                             ApprovalChainResolver chain, ChangeResubmissionService resubmit, AuditService audit,
                             TicketSlaService sla)
    {
        _db = db;
        _files = files;
        _current = current;
        _notify = notify;
        _escalation = escalation;
        _authority = authority;
        _chain = chain;
        _resubmit = resubmit;
        _audit = audit;
        _sla = sla;
    }

    private AppUser Me => _current.User!;

    // Ekip görünürlüğü (TicketScope): görülemeyen talep "bulunamadı" sayılır
    private Task<bool> CanSeeAsync(int id) => _db.Tickets.VisibleTo(Me).AnyAsync(t => t.Id == id);

    // ───── Manuel eskalasyon ─────────────────────────────────────────────
    // SLA dolmasını beklemeden talebi bir sonraki eskalasyon seviyesine
    // yükseltir. Gerekçe zorunlu; hedef kişi seçilmezse kuralın hedefi
    // (rol / ekip lideri / yedek) uygulanır.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Escalate(int id, string? reason, int? targetUserId)
    {
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length < 5 || reason.Length > 250)
        {
            TempData["Err"] = "Eskalasyon gerekçesi 5–250 karakter olmalı.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!await CanSeeAsync(id)) return NotFound();

        var (ok, message) = await _escalation.EscalateManuallyAsync(id, _current.User!.Id, reason, targetUserId);
        TempData[ok ? "Ok" : "Err"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    // ───── Onay sonrası uygulama / geri alma ─────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateImplementation(int id, ImplementationStatus status, string? note, string? rowVersion)
    {
        var ticket = await _db.Tickets.Include(t => t.ChangeDetail).FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null || !TicketScope.CanSee(Me, ticket)) return NotFound();
        if (TicketConcurrency.Expect(_db, ticket, rowVersion)) return await ConflictAsync(id);

        var karar = ChangeImplementationRules.CanMove(ticket, status, AppTime.UtcNow);
        if (!karar.Allowed)
        {
            TempData["Err"] = karar.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        // Başarısızlık ve geri alma not ister: denetimde "neden" sorusunun cevabı
        if (status is ImplementationStatus.Failed or ImplementationStatus.RolledBack && string.IsNullOrWhiteSpace(note))
        {
            TempData["Err"] = "Başarısız uygulama ve geri alma için açıklama zorunludur.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var d = ticket.ChangeDetail!;
        var eski = d.ImplementationStatus;
        ChangeImplementationRules.Apply(d, status, AppTime.UtcNow, note?[..Math.Min(1000, note.Length)]);
        d.UpdatedAt = AppTime.UtcNow;
        d.UpdatedById = _current.User!.Id;

        _db.TicketHistories.Add(new TicketHistory
        {
            TicketId = id, Field = "Uygulama",
            OldValue = Ui.ImplementationText(eski), NewValue = Ui.ImplementationText(status),
            ChangedById = _current.User!.Id, ChangedAt = AppTime.UtcNow
        });

        // Uygulama başlayınca talep işleme alınır; uygulandıysa çözülür
        if (status == ImplementationStatus.InProgress && ticket.Status == TicketStatus.New
            && TicketWorkflow.CanTransition(ticket, TicketStatus.InProgress).Allowed)
            await _sla.ApplyStatusAsync(ticket, TicketStatus.InProgress, AppTime.UtcNow);
        if (status is ImplementationStatus.Implemented or ImplementationStatus.RolledBack
            && TicketWorkflow.IsOpen(ticket.Status)
            && TicketWorkflow.CanTransition(ticket, TicketStatus.Resolved).Allowed)
        {
            // Otomatik çözümde zorunlu çözüm kodu uygulama sonucundan doldurulur
            ticket.ResolutionCode ??= ResolutionCode.ChangeImplemented;
            ticket.RootCause ??= string.IsNullOrWhiteSpace(note)
                ? $"Değişiklik: {Ui.ImplementationText(status)}"
                : note[..Math.Min(1000, note.Length)];
            await _sla.ApplyStatusAsync(ticket, TicketStatus.Resolved, AppTime.UtcNow);
        }

        _audit.Queue(AuditAction.ChangeImplementationUpdated, _current.User!.Id,
            detail: $"{Ui.ImplementationText(eski)} → {Ui.ImplementationText(status)}. {note}", ticketId: id);

        _notify.Queue(ticket.RequesterId, NotificationType.ChangeImplementation,
            $"Değişiklik {Ui.ImplementationText(status).ToLowerInvariant()}: {Ui.TicketNo(ticket)} {ticket.Title}",
            string.IsNullOrWhiteSpace(note) ? $"Uygulama durumu: {Ui.ImplementationText(status)}." : note!,
            ticket.Id, EmailTemplates.ChangeImplementation);

        TicketConcurrency.Touch(_db, ticket);
        if (!await TrySaveAsync()) return await ConflictAsync(id);
        _notify.SignalDelivery();

        TempData["Ok"] = $"Uygulama durumu \"{Ui.ImplementationText(status)}\" olarak güncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ───── Reddedileni düzeltip yeniden gönderme (destek talep adına) ────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resubmit(int id, string? correctionNote, string? description,
        RiskLevel? risk, DateTime? plannedStart, DateTime? plannedEnd, string? rowVersion)
    {
        var ticket = await _db.Tickets.Include(t => t.ChangeDetail).Include(t => t.Approvals)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null || !TicketScope.CanSee(Me, ticket)) return NotFound();
        if (TicketConcurrency.Expect(_db, ticket, rowVersion)) return await ConflictAsync(id);

        var (ok, message) = await _resubmit.ResubmitAsync(ticket, _current.User!, correctionNote ?? "",
            description, risk,
            plannedStart == null ? null : AppTime.ToUtc(plannedStart.Value),
            plannedEnd == null ? null : AppTime.ToUtc(plannedEnd.Value));

        TempData[ok ? "Ok" : "Err"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    // Filtreler: arama metni, durum, öncelik, tür, kategori, oluşturma tarihi aralığı
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> Index(string? q, TicketStatus? status, Priority? priority,
        TicketType? type, int? categoryId, DateTime? from, DateTime? to, string? tag, int page = 1)
    {
        var query = _db.Tickets.Include(t => t.Assignee).Include(t => t.Category).VisibleTo(Me);
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var etiket = TicketTags.Normalize(tag);
            query = query.Where(t => t.Tags != null && ("," + t.Tags + ",").Contains("," + etiket + ","));
        }
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(t => t.Title.Contains(q) || t.RequesterName.Contains(q)
                || (t.Assignee != null && (t.Assignee.FirstName + " " + t.Assignee.LastName).Contains(q)));
        if (status != null) query = query.Where(t => t.Status == status);
        if (priority != null) query = query.Where(t => t.Priority == priority);
        if (type != null) query = query.Where(t => t.Type == type);
        if (categoryId != null) query = query.Where(t => t.CategoryId == categoryId);

        // Tarih aralığı yerel gün olarak gelir; UTC'ye çevrilerek CreatedAt ile karşılaştırılır
        if (from is { } fromLocal)
        {
            var fromUtc = AppTime.ToUtc(fromLocal.Date);
            query = query.Where(t => t.CreatedAt >= fromUtc);
        }
        if (to is { } toLocal)
        {
            var toUtc = AppTime.ToUtc(toLocal.Date.AddDays(1));
            query = query.Where(t => t.CreatedAt < toUtc);
        }

        ViewBag.Q = q; ViewBag.Status = status; ViewBag.Priority = priority; ViewBag.Type = type;
        ViewBag.CategoryId = categoryId;
        ViewBag.Tag = tag;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.Categories = await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();

        // İhlal işaretleme ve eskalasyon artık SlaMonitorService'in işi;
        // bu sayfa yalnızca okur. Liste sayfalanır (Paging.DefaultPageSize).
        var (list, pager) = await query.AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ToPageAsync(page);

        ViewBag.Pager = pager;
        return View(list);
    }

    // Soft-delete edilmiş talepler (ISO 9001 md. 7.5: kayıt korunur, listeden
    // kalkar). Global query filter IgnoreQueryFilters ile aşılır.
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> Archived(string? q, int page = 1)
    {
        var query = _db.Tickets.IgnoreQueryFilters().AsNoTracking()
            .Include(t => t.DeletedBy)
            .Include(t => t.Category)
            .Where(t => t.IsDeleted)
            .VisibleTo(Me);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ara = q.Trim();
            query = query.Where(t => t.Title.Contains(ara) || t.RequesterName.Contains(ara)
                || (t.Number != null && t.Number.Contains(ara))
                || (t.DeleteReason != null && t.DeleteReason.Contains(ara)));
        }

        ViewBag.Q = q;
        var (list, pager) = await query
            .OrderByDescending(t => t.DeletedAt)
            .ToPageAsync(page);

        ViewBag.Pager = pager;
        return View(list);
    }

    public async Task<IActionResult> Details(int id)
    {
        // Arşivlenmiş talepler de açılsın diye filtre yok sayılır; IsDeleted
        // ise görünüm salt okunur moda geçer.
        var ticket = await _db.Tickets
            .IgnoreQueryFilters()
            .Include(t => t.Survey)!.ThenInclude(r => r!.Answers).ThenInclude(a => a.Question)
            .Include(t => t.Attachments)
            .Include(t => t.Assignee)
            .Include(t => t.Requester)
            .Include(t => t.DeletedBy)
            .Include(t => t.Comments).ThenInclude(c => c.Author)
            .Include(t => t.Comments).ThenInclude(c => c.Attachments)
            .Include(t => t.History).ThenInclude(h => h.ChangedBy)
            .Include(t => t.Escalations).ThenInclude(e => e.EscalatedTo)
            .Include(t => t.Escalations).ThenInclude(e => e.TriggeredBy)
            .Include(t => t.SlaPolicy)
            .Include(t => t.Category)
            .Include(t => t.AssignedTeam)
            .Include(t => t.ChangeDetail)
            .Include(t => t.Approvals).ThenInclude(a => a.Approver)
            .Include(t => t.Approvals).ThenInclude(a => a.OnBehalfOf)
            .Include(t => t.Followers).ThenInclude(f => f.User)
            .AsNoTracking()
            // Birden fazla koleksiyon include ediliyor: tek sorgu kartezyen
            // çarpım üretir, ayrı sorgulara bölünür
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null || !TicketScope.CanSee(Me, ticket)) return NotFound();

        ticket.Comments = ticket.Comments.OrderBy(c => c.CreatedAt).ToList();
        ticket.History = ticket.History.OrderByDescending(h => h.ChangedAt).ToList();
        ticket.Escalations = ticket.Escalations.OrderByDescending(e => e.OccurredAt).ToList();

        // Onay düğmeleri ve manuel eskalasyon formu için
        ViewBag.ApprovalContext = await _authority.ContextForAsync(_current.User!);
        ViewBag.MaxResubmissions = _resubmit.MaxResubmissions;
        ViewBag.EscalationStaff = ticket.IsDeleted
            ? new List<AppUser>()
            : await _db.Users.AsNoTracking()
                .Where(u => u.IsActive && u.Role != UserRole.User && u.Id != ticket.AssigneeId)
                .OrderBy(u => u.Role).ThenBy(u => u.FirstName).ToListAsync();

        // Takipçi (CC) olarak eklenebilecek aktif kullanıcılar
        var takipciler = ticket.Followers.Select(f => f.UserId).ToList();
        ViewBag.FollowerCandidates = ticket.IsDeleted
            ? new List<AppUser>()
            : await _db.Users.AsNoTracking()
                .Where(u => u.IsActive && u.Id != ticket.RequesterId && !takipciler.Contains(u.Id))
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();

        // Çözüm makalesi seçimi için yayımlanmış makaleler (arşivde gerekmez)
        ViewBag.Articles = ticket.IsDeleted
            ? new List<KnowledgeArticle>()
            : await _db.KnowledgeArticles.AsNoTracking()
                .Where(a => a.IsPublished)
                .OrderBy(a => a.Title)
                .ToListAsync();

        return View(ticket);
    }

    // Durum butonları: İşleme al / Çözüldü / Kapat / Yeniden aç.
    // Geçişe izin verilip verilmediği TicketWorkflow'da kontrol edilir.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, TicketStatus status, string? rowVersion,
        ResolutionCode? resolutionCode = null, string? rootCause = null)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null || !TicketScope.CanSee(Me, ticket)) return NotFound();
        if (TicketConcurrency.Expect(_db, ticket, rowVersion)) return await ConflictAsync(id);

        var karar = TicketWorkflow.CanTransition(ticket, status);
        if (!karar.Allowed)
        {
            TempData["Err"] = karar.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        // Kapanışta çözüm kodu + kök neden zorunlu
        var cozumHatasi = TicketWorkflow.ValidateResolution(ticket, status, resolutionCode, rootCause);
        if (cozumHatasi != null)
        {
            TempData["Err"] = cozumHatasi;
            return RedirectToAction(nameof(Details), new { id });
        }
        if (TicketWorkflow.IsFinished(status) && ticket.ResolutionCode == null)
        {
            ticket.ResolutionCode = resolutionCode;
            ticket.RootCause = rootCause!.Trim();
        }

        // Kimseye atanmamış talebi işleme alan kişi talebi üstlenir
        if (status == TicketStatus.InProgress && ticket.AssigneeId == null)
            ticket.AssigneeId = _current.User!.Id;

        // "Müşteri bekleniyor"dan çıkışta SLA sayacı mesai takvimine göre devam eder
        await _sla.ApplyStatusAsync(ticket, status, AppTime.UtcNow);
        if (!await TrySaveAsync()) return await ConflictAsync(id);

        TempData["Ok"] = karar.Reopened
            ? "Talep yeniden açıldı."
            : $"Talep durumu \"{Ui.StatusText(status)}\" olarak güncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Atama ekranı: aktif destek personeli ve her birinin üzerindeki açık talep sayısı
    public async Task<IActionResult> Assign(int id)
    {
        var ticket = await _db.Tickets.AsNoTracking()
            .Include(t => t.Assignee).Include(t => t.AssignedTeam).Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null || !TicketScope.CanSee(Me, ticket)) return NotFound();

        var staff = await _db.Users.AsNoTracking().Include(u => u.Team)
            .Where(u => u.Role != UserRole.User && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .ToListAsync();

        // Talebin ekibindeki kişiler listenin başına gelsin
        ViewBag.Staff = staff
            .OrderByDescending(u => u.TeamId != null && u.TeamId == ticket.AssignedTeamId)
            .ToList();

        // Kişi başına açık (Yeni + İşlemde) talep sayısı: iş yükünü görmek için
        ViewBag.OpenCounts = await _db.Tickets
            .Where(t => t.AssigneeId != null && (t.Status == TicketStatus.New || t.Status == TicketStatus.InProgress
                                                 || t.Status == TicketStatus.WaitingForCustomer))
            .GroupBy(t => t.AssigneeId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        return View(ticket);
    }

    // Talebi seçilen personele atar. assigneeId boş gelirse atama kaldırılır.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, int? assigneeId, string? rowVersion)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null || !TicketScope.CanSee(Me, ticket)) return NotFound();
        if (TicketConcurrency.Expect(_db, ticket, rowVersion)) return await ConflictAsync(id);

        // Talep yalnızca aktif destek personeline atanabilir
        if (assigneeId != null &&
            !await _db.Users.AnyAsync(u => u.Id == assigneeId && u.IsActive && u.Role != UserRole.User))
        {
            TempData["Err"] = "Seçilen kişi bu talebe atanamaz.";
            return RedirectToAction(nameof(Assign), new { id });
        }

        if (ticket.AssigneeId != assigneeId)
        {
            ticket.AssigneeId = assigneeId;

            // Yeni atanan kişiye bildirim (kendini atadıysa gerek yok)
            if (assigneeId != null && assigneeId != _current.User!.Id)
                _notify.Queue(assigneeId.Value, NotificationType.Assigned,
                    $"Size atandı: {Ui.TicketNo(ticket)} {ticket.Title}",
                    $"Hedef çözüm: {AppTime.ToLocal(ticket.SlaDueAt):dd.MM.yyyy HH:mm}", ticket.Id);

            if (!await TrySaveAsync()) return await ConflictAsync(id);
            _notify.SignalDelivery();
        }

        TempData["Ok"] = assigneeId == null ? "Atama kaldırıldı." : "Talep atandı.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Değişiklik talebinin planını kaydeder. Risk "Yüksek" seçilirse onay
    // zinciri iki adıma çıkar, bu yüzden plan onaydan önce doldurulmalıdır.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateChangeDetail(
        int id, RiskLevel risk, DateTime? plannedStart, DateTime? plannedEnd,
        string? implementationPlan, string? rollbackPlan, string? impactAnalysis, string? rowVersion,
        string? cabMeetingUrl = null, DateTime? cabMeetingAt = null)
    {
        if (plannedStart != null && plannedEnd != null && plannedEnd <= plannedStart)
        {
            TempData["Err"] = "Planlanan bitiş, başlangıçtan sonra olmalı.";
            return RedirectToAction(nameof(Details), new { id });
        }

        cabMeetingUrl = string.IsNullOrWhiteSpace(cabMeetingUrl) ? null : cabMeetingUrl.Trim();
        if (cabMeetingUrl != null && (cabMeetingUrl.Length > 500
            || !Uri.TryCreate(cabMeetingUrl, UriKind.Absolute, out var cabUri)
            || (cabUri.Scheme != Uri.UriSchemeHttps && cabUri.Scheme != Uri.UriSchemeHttp)))
        {
            TempData["Err"] = "CAB toplantı kaydı geçerli bir http(s) bağlantısı olmalı.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var ticket = await _db.Tickets
            .Include(t => t.ChangeDetail)
            .Include(t => t.Approvals)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null || !TicketScope.CanSee(Me, ticket)) return NotFound();
        if (TicketConcurrency.Expect(_db, ticket, rowVersion)) return await ConflictAsync(id);

        if (ticket.Type != TicketType.Change)
        {
            TempData["Err"] = "Değişiklik planı yalnızca Değişiklik taleplerinde tutulur.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var detail = ticket.ChangeDetail;

        // Onay başladıktan sonra risk değiştirilemez: risk, onay adımı sayısını
        // belirliyor; sonradan değişirse verilmiş onaylar anlamını yitirir
        if (TicketWorkflow.IsApprovalStarted(ticket) && risk != (detail?.Risk ?? RiskLevel.Low))
        {
            TempData["Err"] = "Onay süreci başladığı için risk seviyesi değiştirilemez.";
            return RedirectToAction(nameof(Details), new { id });
        }
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
        detail.CabMeetingUrl = cabMeetingUrl;
        detail.CabMeetingAt = cabMeetingAt == null ? null : AppTime.ToUtc(cabMeetingAt.Value);
        detail.UpdatedAt = AppTime.UtcNow;
        detail.UpdatedById = _current.User!.Id;

        // Onay başlamadıysa zincir kuralları (kategori / öncelik / risk) yeniden çözülür
        if (ticket.ApprovalStatus == ApprovalStatus.Pending && !TicketWorkflow.IsApprovalStarted(ticket))
            await _chain.ApplyAsync(ticket);

        // Plan ayrı tabloda; talebin sürümü yine de ilerlemeli ki aynı planı
        // açık tutan diğer kişi üzerine yazamasın
        TicketConcurrency.Touch(_db, ticket);
        if (!await TrySaveAsync()) return await ConflictAsync(id);

        TempData["Ok"] = "Değişiklik planı kaydedildi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Talebi kalıcı silmek yerine pasifleştirir: kayıt veritabanında kalır,
    // sorgulara düşmez. Yalnızca yönetici yapabilir (ISO 9001 md. 7.5).
    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> SoftDelete(int id, string? reason, string? rowVersion)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();
        if (TicketConcurrency.Expect(_db, ticket, rowVersion)) return await ConflictAsync(id);

        // Neden zorunlu ve talebin üzerine kaydedilir
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > 500)
        {
            TempData["Err"] = "Arşivleme nedeni boş olamaz ve en fazla 500 karakter olabilir.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ticket.IsDeleted = true;
        ticket.DeletedAt = AppTime.UtcNow;
        ticket.DeletedById = _current.User!.Id;
        ticket.DeleteReason = reason;

        _db.TicketHistories.Add(new TicketHistory
        {
            TicketId = ticket.Id,
            Field = "Kayıt",
            OldValue = "Aktif",
            NewValue = "Arşivlendi",
            ChangedById = _current.User!.Id,
            ChangedAt = AppTime.UtcNow
        });

        if (!await TrySaveAsync()) return await ConflictAsync(id);

        TempData["Ok"] = $"{Ui.TicketNo(ticket)} arşivlendi. Kayıt silinmedi; Arşiv ekranından görülebilir.";
        return RedirectToAction(nameof(Archived));
    }

    // Destek personelinin yazışması. İç not işaretlenirse kullanıcı göremez.
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> AddComment(int id, string? body, bool isInternal, int? workMinutes, List<IFormFile>? files)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null || !TicketScope.CanSee(Me, ticket)) return NotFound();

        // Kapanmış talebin yazışması kapalıdır: kayıt kapandıktan sonra
        // üzerine not düşülmesi denetim izini bulanıklaştırır (ISO 9001 md. 7.5).
        // Konuşmaya devam edilecekse talep önce yeniden açılır.
        if (ticket.Status == TicketStatus.Closed)
        {
            TempData["Err"] = "Kapanmış talebe mesaj yazılamaz. Gerekiyorsa talebi yeniden açın.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (string.IsNullOrWhiteSpace(body) || body.Length > 4000)
        {
            TempData["Err"] = "Mesaj boş olamaz ve en fazla 4000 karakter olabilir.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Harcanan süre boş bırakılabilir; girildiyse 1–10000 arası tam sayı olmalı.
        // "abc", "1,5" gibi sayı olmayan değerler ModelState hatası olarak gelir.
        if (!ModelState.IsValid || workMinutes is < 1 or > 10000)
        {
            TempData["Err"] = "Harcanan süre 1 ile 10000 dakika arasında bir tam sayı olmalı.";
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
        TicketWorkflow.MarkFirstResponse(ticket, isInternal, AppTime.UtcNow);

        // Harcanan süre talep bazında toplanır
        ticket.WorkMinutesTotal += workMinutes ?? 0;

        // Kullanıcıya yazılan yanıt talep sahibine ve takipçilere (CC) bildirilir; iç not bildirilmez
        if (!isInternal)
        {
            _notify.Queue(ticket.RequesterId, NotificationType.NewComment,
                $"Talebinize yanıt: {Ui.TicketNo(ticket)} {ticket.Title}",
                $"{_current.User!.FullName} yanıt yazdı.", ticket.Id);

            var takipciler = await _db.TicketFollowers.AsNoTracking()
                .Where(f => f.TicketId == id && f.UserId != ticket.RequesterId && f.UserId != Me.Id)
                .Select(f => f.UserId).ToListAsync();
            foreach (var uid in takipciler)
                _notify.Queue(uid, NotificationType.NewComment,
                    $"Takip ettiğiniz talepte yanıt: {Ui.TicketNo(ticket)} {ticket.Title}",
                    $"{_current.User!.FullName} yanıt yazdı.", ticket.Id);
        }

        // Mesaj eklemek bir düzenleme değil, eklemedir: arada talep başkası
        // tarafından güncellendiyse mesaj kaybolmamalı. Talebin güncel hali
        // okunur, ilk yanıt işareti yeniden uygulanır ve bir kez daha denenir.
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await _db.Entry(ticket).ReloadAsync();
            TicketWorkflow.MarkFirstResponse(ticket, isInternal, AppTime.UtcNow);
            ticket.WorkMinutesTotal += workMinutes ?? 0;
            await _db.SaveChangesAsync();
        }

        TempData["Ok"] = isInternal ? "İç not eklendi." : "Yanıtınız talebe eklendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ───── Öncelik değişikliği: SLA hedefleri yeniden hesaplanır ─────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePriority(int id, Priority priority, string? rowVersion)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null || !TicketScope.CanSee(Me, ticket)) return NotFound();
        if (TicketConcurrency.Expect(_db, ticket, rowVersion)) return await ConflictAsync(id);

        if (TicketWorkflow.IsFinished(ticket.Status))
        {
            TempData["Err"] = "Çözülmüş veya kapanmış talebin önceliği değiştirilemez.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (ticket.Priority == priority) return RedirectToAction(nameof(Details), new { id });

        ticket.Priority = priority;            // geçmişe AppDbContext yazar
        await _sla.RecalculateAsync(ticket);   // yeni önceliğin politikasıyla hedefler

        if (!await TrySaveAsync()) return await ConflictAsync(id);

        TempData["Ok"] = $"Öncelik \"{Ui.PriorityText(priority)}\" yapıldı. Yeni SLA bitişi: " +
                         $"{AppTime.ToLocal(ticket.SlaDueAt):dd.MM.yyyy HH:mm}";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ───── "Ben alıyorum": atanmamış talebi kilitleyerek üstlenme ────────
    // Tek bir UPDATE ... WHERE AssigneeId IS NULL ile yapılır: iki kişi aynı
    // anda basarsa yalnızca biri talebi alır, diğeri uyarı görür.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(int id)
    {
        var ticket = await _db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null || !TicketScope.CanSee(Me, ticket)) return NotFound();

        if (TicketWorkflow.IsFinished(ticket.Status))
        {
            TempData["Err"] = "Çözülmüş veya kapanmış talep üstlenilemez.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var meId = Me.Id;
        var alindi = await _db.Tickets
            .Where(t => t.Id == id && t.AssigneeId == null)
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.AssigneeId, (int?)meId));

        if (alindi == 0)
        {
            TempData["Err"] = "Bu talep az önce başka biri tarafından üstlenildi.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Toplu güncelleme SaveChanges'ten geçmediği için geçmiş elle yazılır
        _db.TicketHistories.Add(new TicketHistory
        {
            TicketId = id, Field = "Atanan", OldValue = "Atanmadı", NewValue = Me.FullName,
            ChangedById = meId, ChangedAt = AppTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Talebi üstlendiniz.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ───── Toplu atama (backend) ─────────────────────────────────────────
    // POST /Tickets/BulkAssign  { "ticketIds": [1,2,3], "assigneeId": 5 }
    // assigneeId boş gelirse seçilen taleplerin ataması kaldırılır.
    // JSON gövdeli istek olduğu için tarayıcı başka siteden gönderemez (CORS).
    public record BulkAssignRequest(List<int>? TicketIds, int? AssigneeId);

    [HttpPost]
    public async Task<IActionResult> BulkAssign([FromBody] BulkAssignRequest? req)
    {
        var ids = req?.TicketIds?.Distinct().ToList() ?? new List<int>();
        if (ids.Count == 0) return BadRequest(new { error = "En az bir talep seçin." });
        if (ids.Count > 200) return BadRequest(new { error = "Tek seferde en fazla 200 talep atanabilir." });

        var assigneeId = req!.AssigneeId;
        if (assigneeId != null &&
            !await _db.Users.AnyAsync(u => u.Id == assigneeId && u.IsActive && u.Role != UserRole.User))
            return BadRequest(new { error = "Seçilen kişiye talep atanamaz." });

        var tickets = await _db.Tickets.VisibleTo(Me).Where(t => ids.Contains(t.Id)).ToListAsync();
        var atanan = new List<int>();

        foreach (var t in tickets)
        {
            // Kapanmış talep ve zaten aynı kişide olan talep atlanır
            if (TicketWorkflow.IsFinished(t.Status) || t.AssigneeId == assigneeId) continue;

            t.AssigneeId = assigneeId;
            atanan.Add(t.Id);

            if (assigneeId != null && assigneeId != Me.Id)
                _notify.Queue(assigneeId.Value, NotificationType.Assigned,
                    $"Size atandı: {Ui.TicketNo(t)} {t.Title}",
                    $"Hedef çözüm: {AppTime.ToLocal(t.SlaDueAt):dd.MM.yyyy HH:mm}", t.Id);
        }

        if (atanan.Count > 0)
        {
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                TicketConcurrency.DiscardChanges(_db);
                return Conflict(new { error = "Seçilen taleplerden biri bu sırada başkası tarafından güncellendi; tekrar deneyin." });
            }
            _notify.SignalDelivery();
        }

        return Json(new
        {
            assigned = atanan.Count,
            assignedIds = atanan,
            skippedIds = ids.Except(atanan).ToList()
        });
    }

    // ───── Arşivden geri yükleme ─────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Restore(int id)
    {
        var ticket = await _db.Tickets.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted);
        if (ticket == null) return NotFound();

        ticket.IsDeleted = false;
        ticket.DeletedAt = null;
        ticket.DeletedById = null;
        ticket.DeleteReason = null;

        _db.TicketHistories.Add(new TicketHistory
        {
            TicketId = ticket.Id, Field = "Kayıt", OldValue = "Arşivlendi", NewValue = "Geri yüklendi",
            ChangedById = Me.Id, ChangedAt = AppTime.UtcNow
        });

        if (!await TrySaveAsync()) return await ConflictAsync(id);

        TempData["Ok"] = $"{Ui.TicketNo(ticket)} arşivden geri yüklendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ───── Mükerrer talebi asıl talebe birleştirme ───────────────────────
    // Mükerrer talep "Mükerrer" çözüm koduyla kapanır ve asıl talebe bağlanır;
    // talep sahibi ve takipçileri asıl talebin takipçisi olur. Yazışma ve ekler
    // taşınmaz, kendi kaydında kalır (denetim izi).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Merge(int id, string? target, string? rowVersion)
    {
        var dup = await _db.Tickets.Include(t => t.Followers).FirstOrDefaultAsync(t => t.Id == id);
        if (dup == null || !TicketScope.CanSee(Me, dup)) return NotFound();
        if (TicketConcurrency.Expect(_db, dup, rowVersion)) return await ConflictAsync(id);

        target = target?.Trim();
        var hedefId = int.TryParse(target?.TrimStart('#'), out var n) ? n : 0;
        var main = string.IsNullOrEmpty(target) ? null
            : await _db.Tickets.FirstOrDefaultAsync(t => t.Number == target || t.Id == hedefId);

        string? hata =
            main == null || !TicketScope.CanSee(Me, main) ? "Asıl talep bulunamadı."
            : main.Id == dup.Id ? "Talep kendisiyle birleştirilemez."
            : TicketWorkflow.IsFinished(dup.Status) ? "Çözülmüş veya kapanmış talep birleştirilemez."
            : main.Status == TicketStatus.Closed ? "Asıl talep kapanmış; birleştirme yapılamaz."
            : main.MergedIntoTicketId != null ? "Asıl talep de başka bir talebe birleştirilmiş."
            : !TicketWorkflow.CanTransition(dup, TicketStatus.Closed).Allowed ? "Bu talep kapatılamaz."
            : null;
        if (hata != null)
        {
            TempData["Err"] = hata;
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = AppTime.UtcNow;
        dup.MergedIntoTicketId = main!.Id;
        dup.ResolutionCode = ResolutionCode.Duplicate;
        dup.RootCause = $"{Ui.TicketNo(main)} ile birleştirildi.";
        await _sla.ApplyStatusAsync(dup, TicketStatus.Closed, now);

        var mevcutTakipciler = await _db.TicketFollowers
            .Where(f => f.TicketId == main.Id).Select(f => f.UserId).ToListAsync();
        var yeniTakipciler = dup.Followers.Select(f => f.UserId).Append(dup.RequesterId)
            .Where(u => u != main.RequesterId && !mevcutTakipciler.Contains(u))
            .Distinct();
        foreach (var uid in yeniTakipciler)
            _db.TicketFollowers.Add(new TicketFollower { TicketId = main.Id, UserId = uid, AddedAt = now });

        _db.TicketComments.Add(new TicketComment
        {
            TicketId = dup.Id, AuthorId = Me.Id, IsInternal = false, CreatedAt = now,
            Body = $"Bu talep {Ui.TicketNo(main)} numaralı talep ile birleştirildi. Gelişmeler o talep üzerinden yürütülecek."
        });
        _db.TicketComments.Add(new TicketComment
        {
            TicketId = main.Id, AuthorId = Me.Id, IsInternal = true, CreatedAt = now,
            Body = $"{Ui.TicketNo(dup)} ({dup.RequesterName}) bu talebe birleştirildi: {dup.Title}"
        });

        _notify.Queue(dup.RequesterId, NotificationType.NewComment,
            $"Talebiniz birleştirildi: {Ui.TicketNo(dup)}",
            $"Talebiniz {Ui.TicketNo(main)} ile birleştirildi.", dup.Id);

        TicketConcurrency.Touch(_db, main);
        if (!await TrySaveAsync()) return await ConflictAsync(id);
        _notify.SignalDelivery();

        TempData["Ok"] = $"{Ui.TicketNo(dup)}, {Ui.TicketNo(main)} ile birleştirildi.";
        return RedirectToAction(nameof(Details), new { id = main.Id });
    }

    // ───── Takipçi (CC) ──────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFollower(int id, int userId)
    {
        var ticket = await _db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null || !TicketScope.CanSee(Me, ticket)) return NotFound();

        if (!await _db.Users.AnyAsync(u => u.Id == userId && u.IsActive))
            TempData["Err"] = "Seçilen kullanıcı takipçi olarak eklenemez.";
        else if (userId == ticket.RequesterId)
            TempData["Err"] = "Talep sahibi zaten bilgilendiriliyor.";
        else if (await _db.TicketFollowers.AnyAsync(f => f.TicketId == id && f.UserId == userId))
            TempData["Err"] = "Bu kullanıcı zaten takipçi.";
        else
        {
            _db.TicketFollowers.Add(new TicketFollower { TicketId = id, UserId = userId });
            try
            {
                await _db.SaveChangesAsync();
                TempData["Ok"] = "Takipçi eklendi.";
            }
            catch (DbUpdateException ex) when (AppDbContext.IsUniqueViolation(ex))
            {
                TempData["Err"] = "Bu kullanıcı zaten takipçi.";
            }
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFollower(int id, int userId)
    {
        if (!await CanSeeAsync(id)) return NotFound();

        var f = await _db.TicketFollowers.FirstOrDefaultAsync(x => x.TicketId == id && x.UserId == userId);
        if (f != null)
        {
            _db.TicketFollowers.Remove(f);
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Takipçi çıkarıldı.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // ───── Etiketler ─────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTags(int id, string? tags, string? rowVersion)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null || !TicketScope.CanSee(Me, ticket)) return NotFound();
        if (TicketConcurrency.Expect(_db, ticket, rowVersion)) return await ConflictAsync(id);

        var (value, error) = TicketTags.Parse(tags);
        if (error != null)
        {
            TempData["Err"] = error;
            return RedirectToAction(nameof(Details), new { id });
        }

        ticket.Tags = value;
        if (!await TrySaveAsync()) return await ConflictAsync(id);

        TempData["Ok"] = "Etiketler kaydedildi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ───── Portaldan gelen iptal isteğine karar ──────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideCancel(int id, bool approve, string? note, string? rowVersion)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null || !TicketScope.CanSee(Me, ticket)) return NotFound();
        if (TicketConcurrency.Expect(_db, ticket, rowVersion)) return await ConflictAsync(id);

        note = note?.Trim();
        string? hata =
            ticket.CancelRequestedAt == null ? "Bu talepte bekleyen bir iptal isteği yok."
            : !approve && string.IsNullOrEmpty(note) ? "İptal isteğini reddederken gerekçe yazın."
            : note is { Length: > 500 } ? "Not en fazla 500 karakter olabilir."
            : approve && !TicketWorkflow.CanTransition(ticket, TicketStatus.Closed).Allowed ? "Bu talep kapatılamaz."
            : null;
        if (hata != null)
        {
            TempData["Err"] = hata;
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = AppTime.UtcNow;
        ticket.CancelRequestedAt = null;   // karar verildi; neden CancelReason'da kalır

        if (approve)
        {
            ticket.ResolutionCode = ResolutionCode.Cancelled;
            var neden = $"Kullanıcı isteğiyle iptal: {ticket.CancelReason}";
            ticket.RootCause = neden.Length > 1000 ? neden[..1000] : neden;
            await _sla.ApplyStatusAsync(ticket, TicketStatus.Closed, now);
        }

        _db.TicketHistories.Add(new TicketHistory
        {
            TicketId = id, Field = "İptal isteği", OldValue = "Bekliyor",
            NewValue = approve ? "Onaylandı" : "Reddedildi", ChangedById = Me.Id, ChangedAt = now
        });

        var mesaj = approve
            ? "İptal isteğiniz onaylandı, talep kapatıldı." + (string.IsNullOrEmpty(note) ? "" : $" Not: {note}")
            : $"İptal isteğiniz reddedildi. Gerekçe: {note}";
        _db.TicketComments.Add(new TicketComment
        {
            TicketId = id, AuthorId = Me.Id, IsInternal = false, Body = mesaj, CreatedAt = now
        });
        _notify.Queue(ticket.RequesterId, NotificationType.NewComment,
            $"İptal isteği {(approve ? "onaylandı" : "reddedildi")}: {Ui.TicketNo(ticket)}", mesaj, ticket.Id);

        if (!await TrySaveAsync()) return await ConflictAsync(id);
        _notify.SignalDelivery();

        TempData["Ok"] = approve ? "İptal isteği onaylandı, talep kapatıldı." : "İptal isteği reddedildi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Kayıt sırasında yakalanan çakışma (bkz. TicketConcurrency): false döner
    private async Task<bool> TrySaveAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            TicketConcurrency.DiscardChanges(_db);
            return false;
        }
    }

    private async Task<IActionResult> ConflictAsync(int id)
    {
        TempData["Err"] = await TicketConcurrency.ConflictMessageAsync(_db, id, _current.User?.Id);
        return RedirectToAction(nameof(Details), new { id });
    }
}
