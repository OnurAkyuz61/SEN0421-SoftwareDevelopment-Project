using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Services.Approvals;

// Reddedilen değişiklik talebinin düzeltilip yeniden onaya gönderilmesi.
//
// Önceden red nihai karardı: talep kapanıyor ve bir daha açılamıyordu; talep
// sahibi aynı değişikliği baştan yeni bir talep olarak açmak zorundaydı ve
// red gerekçesi ile düzeltme arasındaki bağ kayboluyordu. Artık:
//   • talep yeni bir onay turu açar (ApprovalRound + 1), önceki turun
//     kararları geçmiş olarak kalır;
//   • açıklama / plan düzeltilir, düzeltme notu yazışmaya eklenir;
//   • zincir kuralları yeniden çözülür (düzeltmeyle risk değişmiş olabilir);
//   • en fazla MaxResubmissions kez yapılabilir.
public class ChangeResubmissionService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notify;
    private readonly ApprovalChainResolver _chain;
    private readonly AuditService _audit;
    private readonly ApprovalSettings _cfg;

    public ChangeResubmissionService(AppDbContext db, NotificationService notify, ApprovalChainResolver chain,
                                     AuditService audit, IOptions<ApprovalSettings> cfg)
    {
        _db = db;
        _notify = notify;
        _chain = chain;
        _audit = audit;
        _cfg = cfg.Value;
    }

    public int MaxResubmissions => _cfg.MaxResubmissions;

    // Talep izlenen olmalı (ChangeDetail ve Approvals yüklü). Kaydeder.
    public async Task<(bool Ok, string Message)> ResubmitAsync(
        Ticket t, AppUser by, string correctionNote, string? newDescription,
        RiskLevel? newRisk, DateTime? plannedStartUtc, DateTime? plannedEndUtc)
    {
        var izin = ApprovalPolicy.CanResubmit(t, by, _cfg.MaxResubmissions);
        if (!izin.Allowed) return (false, izin.Error!);

        correctionNote = correctionNote.Trim();
        if (correctionNote.Length < 10)
            return (false, "Düzeltme notu en az 10 karakter olmalı: onaycılar neyin değiştiğini görmeli.");
        if (correctionNote.Length > 2000)
            return (false, "Düzeltme notu en fazla 2000 karakter olabilir.");

        if (plannedStartUtc != null && plannedEndUtc != null && plannedEndUtc <= plannedStartUtc)
            return (false, "Planlanan bitiş, başlangıçtan sonra olmalı.");
        if (plannedEndUtc != null && plannedEndUtc <= AppTime.UtcNow)
            return (false, "Planlanan uygulama penceresi gelecekte olmalı.");

        var oldRound = t.ApprovalRound;
        t.ApprovalRound++;
        t.ApprovalStatus = ApprovalStatus.Pending;
        t.ApprovalNote = null;
        t.ApprovalDecidedAt = AppTime.UtcNow;   // yeni turun başlangıcı (hatırlatma bu andan sayar)
        t.LastApprovalReminderAt = null;
        t.Status = TicketStatus.New;
        t.ResolvedAt = null;
        t.ClosedAt = null;

        if (!string.IsNullOrWhiteSpace(newDescription)) t.Description = newDescription.Trim();

        t.ChangeDetail ??= new ChangeDetail { TicketId = t.Id };
        if (t.ChangeDetail.TicketId == 0) t.ChangeDetail.TicketId = t.Id;
        if (newRisk != null) t.ChangeDetail.Risk = newRisk.Value;
        if (plannedStartUtc != null) t.ChangeDetail.PlannedStart = plannedStartUtc;
        if (plannedEndUtc != null) t.ChangeDetail.PlannedEnd = plannedEndUtc;
        t.ChangeDetail.UpdatedAt = AppTime.UtcNow;
        t.ChangeDetail.UpdatedById = by.Id;
        t.ChangeDetail.ImplementationStatus = ImplementationStatus.NotStarted;

        await _chain.ApplyAsync(t);

        _db.TicketComments.Add(new TicketComment
        {
            TicketId = t.Id,
            AuthorId = by.Id,
            Body = $"[Yeniden onaya gönderildi · {t.ApprovalRound}. tur]\n{correctionNote}",
            IsInternal = false,
            CreatedAt = AppTime.UtcNow
        });
        _db.TicketHistories.Add(new TicketHistory
        {
            TicketId = t.Id, Field = "Onay turu",
            OldValue = oldRound.ToString(), NewValue = t.ApprovalRound.ToString(),
            ChangedById = by.Id, ChangedAt = AppTime.UtcNow
        });

        _audit.Queue(AuditAction.ChangeResubmitted, by.Id, detail: $"{t.ApprovalRound}. tur: {correctionNote}", ticketId: t.Id);

        // İlk adım kimdeyse ona: tek adımlı zincirde yönetici, çok adımlıda destek
        var firstRole = t.RequiredApprovalSteps > 1 ? UserRole.Agent : UserRole.Admin;
        await _notify.QueueToRoleAsync(firstRole, NotificationType.ApprovalPending,
            $"Yeniden onay bekliyor: {Ui.TicketNo(t)} {t.Title}",
            $"{by.FullName} reddedilen değişikliği düzeltip {t.ApprovalRound}. tur için gönderdi: {correctionNote}",
            t.Id, exceptUserId: by.Id, templateKey: EmailTemplates.ApprovalPending);

        if (by.Id != t.RequesterId)
            _notify.Queue(t.RequesterId, NotificationType.ApprovalPending,
                $"Talebiniz yeniden onaya gönderildi: {Ui.TicketNo(t)}",
                $"{by.FullName} talebinizi düzelterek yeniden gönderdi.", t.Id);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TicketConcurrency.DiscardChanges(_db);
            return (false, "Talep bu arada başka biri tarafından güncellendi; sayfayı yenileyip tekrar deneyin.");
        }

        _notify.SignalDelivery();
        return (true, $"{Ui.TicketNo(t)} düzeltildi ve {t.ApprovalRound}. tur için yeniden onaya gönderildi.");
    }
}
