using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Services;

// Uygunsuzlukların otomatik açılması (ISO 9001 md. 10.2).
// SLA ihlali ve eşik altı memnuniyet, kalite kaydı doğuran iki olaydır;
// bunlar elle takip edilmeye bırakılmaz.
//
// Eşikler artık const değil, appsettings.json → "Quality" bölümünden gelir
// (QualitySettings); kararın kendisi saf sınıfta (NonconformityPolicy).
//
// Açılan her kayıt bir SORUMLUYA bağlanır ve sorumluya bildirim gider:
// sahibi olmayan kalite kaydı hiçbir zaman işlenmez, denetimde de "açıldı
// ama kimse görmedi" olarak çıkar. Atanan kişi yoksa sorumluluk ekip
// liderine, oradan birimdeki diğer ekip liderine, en sonunda yönetime
// (Admin) düşer — md. 5.1: kalite sorumluluğu yönetimdedir.
public class NonconformityService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notify;
    private readonly QualitySettings _cfg;

    public NonconformityService(AppDbContext db, NotificationService notify, IOptions<QualitySettings> cfg)
    {
        _db = db;
        _notify = notify;
        _cfg = cfg.Value;
    }

    // Aynı talep ve kaynak için ikinci kayıt açılmaz: aynı olay iki kez
    // kalite kaydı doğurmaz (ör. SLA taraması her dakika aynı talebi görür).
    private Task<bool> ExistsAsync(int ticketId, NonconformitySource source) =>
        _db.Nonconformities.AnyAsync(n => n.TicketId == ticketId && n.Source == source);

    // SaveChanges çağırmaz; çağıran kendi işlemiyle birlikte kaydeder.
    // Bildirim kaydı da aynı DbContext'e yazılır: ya ikisi birlikte olur ya
    // hiçbiri. Kuyruk worker'ını uyandırmak (SignalDelivery) çağıranın işi.
    public async Task<bool> QueueForSlaBreachAsync(Ticket ticket)
    {
        if (await ExistsAsync(ticket.Id, NonconformitySource.SlaBreach)) return false;

        var responsibleId = await ResolveResponsibleAsync(ticket);

        _db.Nonconformities.Add(new Nonconformity
        {
            TicketId = ticket.Id,
            Source = NonconformitySource.SlaBreach,
            Status = NonconformityStatus.Open,
            Description = NonconformityPolicy.Cut(
                $"{Ui.TicketNo(ticket)} talebinde hizmet seviyesi aşıldı: "
                + $"hedef {AppTime.ToLocal(ticket.SlaDueAt):dd.MM.yyyy HH:mm}, çözüm zamanında tamamlanmadı.", 500),
            ResponsibleId = responsibleId,
            DetectedAt = AppTime.UtcNow,
            DueAt = AppTime.UtcNow.AddDays(DueDays)
        });

        Notify(responsibleId, ticket, "SLA ihlali",
            "Çözüm süresi aşıldı; kök neden ve düzeltici faaliyet bekleniyor.");
        return true;
    }

    // Düşük memnuniyet uygunsuzluğu. İki gerekçe var (NonconformityPolicy):
    //   • ortalama eşiğin altında
    //   • TEK bir sorunun kritik düşük puanı — ortalama eşiğin üstünde olsa
    //     bile. 5, 5, 1 veren kullanıcının ortalaması 3,7'dir ama "sorunum
    //     çözüldü mü" sorusuna verdiği 1 puan kalite kaydı doğurmalıdır.
    public async Task<bool> QueueForLowSatisfactionAsync(
        Ticket ticket, double average,
        IReadOnlyList<NonconformityPolicy.AnswerScore> answers, string? comment)
    {
        var finding = NonconformityPolicy.Evaluate(
            average, answers, _cfg.LowSatisfactionThreshold, _cfg.CriticalQuestionScore);

        if (!finding.Triggered) return false;
        if (await ExistsAsync(ticket.Id, NonconformitySource.LowSatisfaction)) return false;

        var responsibleId = await ResolveResponsibleAsync(ticket);

        _db.Nonconformities.Add(new Nonconformity
        {
            TicketId = ticket.Id,
            Source = NonconformitySource.LowSatisfaction,
            Status = NonconformityStatus.Open,
            Description = NonconformityPolicy.Describe(
                Ui.TicketNo(ticket), average, finding, answers, comment, _cfg.LowSatisfactionThreshold),
            ResponsibleId = responsibleId,
            DetectedAt = AppTime.UtcNow,
            DueAt = AppTime.UtcNow.AddDays(DueDays)
        });

        Notify(responsibleId, ticket, "Düşük memnuniyet",
            NonconformityPolicy.NotificationMessage(average, finding));
        return true;
    }

    private int DueDays => Math.Max(1, _cfg.CorrectiveActionDueDays);

    // ───── Sorumlu ve bildirim ──────────────────────────────────────────

    // Sorumluluk zinciri:
    //   1) talebi üstlenen kişi
    //   2) talebin ekibinin lideri
    //   3) aynı birimdeki (talebin ekibi yoksa talep edenin birimi) başka bir
    //      aktif ekibin lideri — "birim yöneticisi" karşılığı; şemada ayrı bir
    //      birim yöneticisi alanı yok
    //   4) yönetici (Admin): kalite sorumluluğu yönetimde kalır (md. 5.1)
    // Aday her adımda aktif ve Destek/Yönetici rolünde olmalı: pasif ya da
    // son kullanıcı hesabı düzeltici faaliyet yürütemez.
    private async Task<int?> ResolveResponsibleAsync(Ticket ticket)
    {
        if (await IsEligibleAsync(ticket.AssigneeId)) return ticket.AssigneeId;

        var teamLeadId = ticket.AssignedTeamId == null
            ? null
            : await _db.Teams.AsNoTracking()
                .Where(t => t.Id == ticket.AssignedTeamId)
                .Select(t => t.LeadUserId)
                .FirstOrDefaultAsync();
        if (await IsEligibleAsync(teamLeadId)) return teamLeadId;

        var departmentId = ticket.AssignedTeamId != null
            ? await _db.Teams.AsNoTracking()
                .Where(t => t.Id == ticket.AssignedTeamId)
                .Select(t => (int?)t.DepartmentId)
                .FirstOrDefaultAsync()
            : await _db.Users.AsNoTracking()
                .Where(u => u.Id == ticket.RequesterId)
                .Select(u => u.DepartmentId)
                .FirstOrDefaultAsync();

        if (departmentId != null)
        {
            // Birimdeki ekip liderleri; sıra kararlı olsun diye ekip adına göre
            var leadIds = await _db.Teams.AsNoTracking()
                .Where(t => t.DepartmentId == departmentId && t.IsActive && t.LeadUserId != null)
                .OrderBy(t => t.Name)
                .Select(t => t.LeadUserId!.Value)
                .ToListAsync();

            foreach (var id in leadIds)
                if (await IsEligibleAsync(id)) return id;
        }

        // Son durak: en eski aktif yönetici hesabı
        return await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Role == UserRole.Admin)
            .OrderBy(u => u.Id)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<bool> IsEligibleAsync(int? userId) =>
        userId != null && await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.IsActive
                           && (u.Role == UserRole.Agent || u.Role == UserRole.Admin));

    // Sorumluya bildirim. Kayıt eklenirken kuyruğa girer (SaveChanges
    // çağıranda), gönderimi MessageQueueWorker yapar.
    private void Notify(int? responsibleId, Ticket ticket, string kind, string message)
    {
        if (responsibleId == null) return;   // atanabilecek kimse yok: kayıt sahipsiz açılır

        _notify.Queue(responsibleId.Value, NotificationType.NonconformityOpened,
            $"Uygunsuzluk açıldı ({kind}): {Ui.TicketNo(ticket)} {ticket.Title}",
            message, ticket.Id, EmailTemplates.NonconformityOpened);
    }
}
