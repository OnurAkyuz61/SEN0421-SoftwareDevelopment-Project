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

    // Filtreler: arama metni, durum, öncelik, tür, kategori, oluşturma tarihi aralığı
    public async Task<IActionResult> Index(string? q, TicketStatus? status, Priority? priority,
        TicketType? type, int? categoryId, DateTime? from, DateTime? to, int page = 1)
    {
        var query = _db.Tickets.Include(t => t.Assignee).Include(t => t.Category).AsQueryable();
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
    public async Task<IActionResult> Archived(string? q, int page = 1)
    {
        var query = _db.Tickets.IgnoreQueryFilters().AsNoTracking()
            .Include(t => t.DeletedBy)
            .Include(t => t.Category)
            .Where(t => t.IsDeleted);

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
            .Include(t => t.SlaPolicy)
            .Include(t => t.Category)
            .Include(t => t.AssignedTeam)
            .Include(t => t.ChangeDetail)
            .Include(t => t.Approvals).ThenInclude(a => a.Approver)
            .AsNoTracking()
            // Birden fazla koleksiyon include ediliyor: tek sorgu kartezyen
            // çarpım üretir, ayrı sorgulara bölünür
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();

        ticket.Comments = ticket.Comments.OrderBy(c => c.CreatedAt).ToList();
        ticket.History = ticket.History.OrderByDescending(h => h.ChangedAt).ToList();
        ticket.Escalations = ticket.Escalations.OrderByDescending(e => e.OccurredAt).ToList();

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
    public async Task<IActionResult> ChangeStatus(int id, TicketStatus status, string? rowVersion)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();
        if (TicketConcurrency.Expect(_db, ticket, rowVersion)) return await ConflictAsync(id);

        var karar = TicketWorkflow.CanTransition(ticket, status);
        if (!karar.Allowed)
        {
            TempData["Err"] = karar.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        // Kimseye atanmamış talebi işleme alan kişi talebi üstlenir
        if (status == TicketStatus.InProgress && ticket.AssigneeId == null)
            ticket.AssigneeId = _current.User!.Id;

        TicketWorkflow.Apply(ticket, status, AppTime.UtcNow);
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
        if (ticket == null) return NotFound();

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
            .Where(t => t.AssigneeId != null && (t.Status == TicketStatus.New || t.Status == TicketStatus.InProgress))
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
        if (ticket == null) return NotFound();
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
            await _notify.DeliverPendingEmailsAsync();
        }

        TempData["Ok"] = assigneeId == null ? "Atama kaldırıldı." : "Talep atandı.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Değişiklik talebinin planını kaydeder. Risk "Yüksek" seçilirse onay
    // zinciri iki adıma çıkar, bu yüzden plan onaydan önce doldurulmalıdır.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateChangeDetail(
        int id, RiskLevel risk, DateTime? plannedStart, DateTime? plannedEnd,
        string? implementationPlan, string? rollbackPlan, string? impactAnalysis, string? rowVersion)
    {
        var ticket = await _db.Tickets
            .Include(t => t.ChangeDetail)
            .Include(t => t.Approvals)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();
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
        detail.UpdatedAt = AppTime.UtcNow;
        detail.UpdatedById = _current.User!.Id;

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
        if (ticket == null) return NotFound();

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

        // Kullanıcıya yazılan yanıt talep sahibine bildirilir; iç not bildirilmez
        if (!isInternal)
            _notify.Queue(ticket.RequesterId, NotificationType.NewComment,
                $"Talebinize yanıt: {Ui.TicketNo(ticket)} {ticket.Title}",
                $"{_current.User!.FullName} yanıt yazdı.", ticket.Id);

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
            await _db.SaveChangesAsync();
        }

        TempData["Ok"] = isInternal ? "İç not eklendi." : "Yanıtınız talebe eklendi.";
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
