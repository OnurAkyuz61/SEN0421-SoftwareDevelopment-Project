using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Services.Escalations;

// Eskalasyon kurallarının saf kısmı (birim testi yazılabilir)
public static class EscalationPolicy
{
    // Öncelik bir kademe artar, Kritik'te durur
    public static Priority Raise(Priority p) => p >= Priority.Critical ? Priority.Critical : p + 1;

    // Gecikmeye göre uygulanması gereken kurallar (seviye sırasıyla)
    public static IEnumerable<EscalationRule> DueRules(IEnumerable<EscalationRule> rules, int currentLevel, int minutesOverdue) =>
        rules.Where(r => r.IsActive && r.Level > currentLevel && minutesOverdue >= r.AfterMinutes)
             .OrderBy(r => r.Level);

    // Manuel eskalasyonda bir sonraki seviye (gecikme şartı aranmaz)
    public static EscalationRule? NextRule(IEnumerable<EscalationRule> rules, int currentLevel) =>
        rules.Where(r => r.IsActive && r.Level > currentLevel).OrderBy(r => r.Level).FirstOrDefault();
}

// Otomatik (SlaMonitorService) ve manuel (TicketsController.Escalate)
// eskalasyonun ortak uygulayıcısı:
//   1. Hedefi bulur: kural türüne göre roldeki en az yüklü kişi ya da
//      talebin ekibinin lideri.
//   2. Hedefte uygun kişi yoksa yedek kurala düşer: önce yedek kişi,
//      sonra yedek rol. O da yoksa atama değişmez, yalnızca bildirim gider.
//   3. Kural istiyorsa önceliği bir kademe artırır.
//   4. Seviyeye özel şablonla bildirim kuyruğa alır, eskalasyon kaydı yazar.
public class EscalationService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notify;
    private readonly AuditService _audit;

    public EscalationService(AppDbContext db, NotificationService notify, AuditService audit)
    {
        _db = db;
        _notify = notify;
        _audit = audit;
    }

    public sealed record Target(AppUser? User, bool UsedFallback, string How);

    // Talep izlenen (tracked) olmalı; AssignedTeam ve LeadUser yüklü olursa
    // ekip lideri hedefi sorgusuz çözülür. SaveChanges ÇAĞIRMAZ.
    public async Task<EscalationRecord> ApplyAsync(
        Ticket t, EscalationRule rule, int minutesOverdue,
        bool manual = false, int? triggeredById = null, string? reason = null,
        int? forcedAssigneeId = null, CancellationToken token = default)
    {
        var now = AppTime.UtcNow;
        var target = await ResolveTargetAsync(t, rule, forcedAssigneeId, token);

        var newAssignee = target.User;
        if (newAssignee != null && newAssignee.Id != t.AssigneeId)
        {
            t.AssigneeId = newAssignee.Id;
            t.Assignee = newAssignee;
            _notify.Queue(newAssignee.Id, NotificationType.Assigned,
                $"Eskalasyonla size atandı: {Ui.TicketNo(t)} {t.Title}",
                manual
                    ? $"Talep elle seviye {rule.Level}'e yükseltildi ve size devredildi. Gerekçe: {reason}"
                    : $"SLA {minutesOverdue} dakika aşıldı; seviye {rule.Level} kuralı gereği talep size devredildi ({target.How}).",
                t.Id, EmailTemplates.Assignment);
        }

        Priority? before = null, after = null;
        if (rule.RaisePriority)
        {
            var raised = EscalationPolicy.Raise(t.Priority);
            if (raised != t.Priority)
            {
                before = t.Priority;
                after = raised;
                t.Priority = raised; // geçmişe AppDbContext otomatik yazar
            }
        }

        var record = new EscalationRecord
        {
            TicketId = t.Id,
            Level = rule.Level,
            OccurredAt = now,
            MinutesOverdue = Math.Max(0, minutesOverdue),
            EscalatedToId = t.AssigneeId,
            IsManual = manual,
            TriggeredById = triggeredById,
            UsedFallback = target.UsedFallback,
            PriorityBefore = before,
            PriorityAfter = after,
            Note = Cut(manual
                ? $"Manuel eskalasyon, seviye {rule.Level}: {reason}"
                : $"{minutesOverdue} dk gecikme, seviye {rule.Level} → {target.How}", 300)
        };
        _db.EscalationRecords.Add(record);

        // Seviyeye özel şablon (1, 2, 3+) ile bildirim
        var template = EmailTemplates.Escalation(rule.Level);
        var title = $"Eskalasyon (seviye {rule.Level}): {Ui.TicketNo(t)} {t.Title}";
        var message = (manual ? $"Elle yükseltildi: {reason}. " : $"Talep {minutesOverdue} dakikadır gecikmede. ")
                      + $"Atanan: {t.Assignee?.FullName ?? "yok"}."
                      + (after != null ? $" Öncelik {Ui.PriorityText(before!.Value)} → {Ui.PriorityText(after.Value)}." : "")
                      + (target.UsedFallback ? " (Hedefte uygun kişi yoktu, yedek kural uygulandı.)" : "");

        if (rule.TargetType == EscalationTarget.TeamLead)
        {
            // Ekip lideri hedefinde role toplu bildirim yerine lider + yöneticiler
            if (t.AssignedTeam?.LeadUserId is int leadId)
                _notify.Queue(leadId, NotificationType.Escalation, title, message, t.Id, template);
            await _notify.QueueToRoleAsync(UserRole.Admin, NotificationType.Escalation, title, message, t.Id,
                exceptUserId: t.AssignedTeam?.LeadUserId, templateKey: template);
        }
        else
        {
            await _notify.QueueToRoleAsync(rule.TargetRole, NotificationType.Escalation, title, message, t.Id,
                templateKey: template);
        }

        t.EscalationLevel = Math.Max(t.EscalationLevel, rule.Level);
        t.IsEscalated = true;
        return record;
    }

    public async Task<Target> ResolveTargetAsync(Ticket t, EscalationRule rule, int? forcedAssigneeId, CancellationToken token)
    {
        if (forcedAssigneeId is int forced)
        {
            var u = await ActiveStaff().FirstOrDefaultAsync(x => x.Id == forced, token);
            if (u != null) return new Target(u, false, "elle seçilen kişi");
        }

        // 1) Birincil hedef
        AppUser? primary = null;
        string how;
        if (rule.TargetType == EscalationTarget.TeamLead)
        {
            how = "ekip lideri";
            var leadId = t.AssignedTeam?.LeadUserId
                         ?? (t.AssignedTeamId == null ? null
                             : await _db.Teams.Where(x => x.Id == t.AssignedTeamId).Select(x => x.LeadUserId).FirstOrDefaultAsync(token));
            if (leadId != null && leadId != t.AssigneeId)
                primary = await ActiveStaff().FirstOrDefaultAsync(u => u.Id == leadId, token);
        }
        else
        {
            how = $"{Ui.RoleText(rule.TargetRole)} rolü";
            primary = await LeastLoadedAsync(rule.TargetRole, t.AssigneeId, token);
        }
        if (primary != null) return new Target(primary, false, how);

        // 2) Yedek kişi
        if (rule.FallbackUserId is int fbId && fbId != t.AssigneeId)
        {
            var fb = await ActiveStaff().FirstOrDefaultAsync(u => u.Id == fbId, token);
            if (fb != null) return new Target(fb, true, "yedek kişi");
        }

        // 3) Yedek rol
        if (rule.FallbackRole is UserRole fbRole && fbRole != UserRole.User)
        {
            var fb = await LeastLoadedAsync(fbRole, t.AssigneeId, token);
            if (fb != null) return new Target(fb, true, $"yedek rol ({Ui.RoleText(fbRole)})");
        }

        return new Target(null, rule.FallbackUserId != null || rule.FallbackRole != null, "uygun kişi yok, atama değişmedi");
    }

    // Manuel eskalasyon: bir sonraki seviyeye elle yükseltir
    public async Task<(bool Ok, string Message)> EscalateManuallyAsync(
        int ticketId, int byUserId, string reason, int? targetUserId)
    {
        var t = await _db.Tickets
            .Include(x => x.Assignee)
            .Include(x => x.AssignedTeam)
            .FirstOrDefaultAsync(x => x.Id == ticketId);
        if (t == null) return (false, "Talep bulunamadı.");

        if (t.Status is TicketStatus.Resolved or TicketStatus.Closed)
            return (false, "Çözülmüş veya kapanmış talep eskale edilemez.");

        var rules = await _db.EscalationRules.AsNoTracking().ToListAsync();
        var rule = EscalationPolicy.NextRule(rules, t.EscalationLevel);

        if (rule == null)
        {
            if (targetUserId == null)
                return (false, "Bu talep tanımlı en üst eskalasyon seviyesinde. Devam etmek için hedef kişi seçin.");

            // Kural kalmadıysa seçilen kişiye bir üst seviye olarak devredilir
            rule = new EscalationRule
            {
                Level = t.EscalationLevel + 1, TargetRole = UserRole.Admin,
                TargetType = EscalationTarget.Role, RaisePriority = true, IsActive = true
            };
        }

        var overdue = (int)Math.Max(0, (AppTime.UtcNow - t.SlaDueAt).TotalMinutes);
        var record = await ApplyAsync(t, rule, overdue, manual: true, triggeredById: byUserId,
            reason: reason, forcedAssigneeId: targetUserId);

        _audit.Queue(AuditAction.ManualEscalation, byUserId, detail: $"Seviye {rule.Level}: {reason}", ticketId: t.Id);
        await _db.SaveChangesAsync();
        _notify.SignalDelivery();

        var kime = t.Assignee?.FullName ?? "atama değişmedi";
        return (true, $"{Ui.TicketNo(t)} seviye {record.Level}'e yükseltildi ({kime}).");
    }

    private IQueryable<AppUser> ActiveStaff() =>
        _db.Users.Where(u => u.IsActive && u.Role != UserRole.User);

    // Roldeki aktif kişilerden üzerinde en az açık talep olan (mevcut sahip hariç)
    private Task<AppUser?> LeastLoadedAsync(UserRole role, int? exceptId, CancellationToken token) =>
        _db.Users
            .Where(u => u.IsActive && u.Role == role && u.Id != exceptId)
            .OrderBy(u => _db.Tickets.Count(x => x.AssigneeId == u.Id
                && (x.Status == TicketStatus.New || x.Status == TicketStatus.InProgress)))
            .ThenBy(u => u.Id)
            .FirstOrDefaultAsync(token);

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;
}
