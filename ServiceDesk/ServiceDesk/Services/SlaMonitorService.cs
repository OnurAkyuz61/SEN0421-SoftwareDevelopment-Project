using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Dakikada bir açık talepleri tarar; süresi yaklaşanı uyarır, aşanı ihlal
// olarak işaretler ve eskalasyon kurallarına göre yükseltir.
//
// Önceden bu iş talep listesi sayfasının GET isteği içinde yapılıyordu:
// listeyi kimse açmazsa eskalasyon hiç oluşmuyordu. Artık uygulama açık
// olduğu sürece kendi başına çalışır.
public class SlaMonitorService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private const int WarningMinutesBefore = 120;   // SLA bitimine 2 saat kala uyar

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SlaMonitorService> _log;

    public SlaMonitorService(IServiceScopeFactory scopes, ILogger<SlaMonitorService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        // Uygulama açılır açılmaz bir tur at, sonra dakikada bir
        do
        {
            try
            {
                await ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Bir tur hata alırsa servis ölmemeli; sonraki turda tekrar dener
                _log.LogError(ex, "SLA taraması başarısız oldu");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    private async Task ScanAsync(CancellationToken token)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notify = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var now = AppTime.UtcNow;
        var warnThreshold = now.AddMinutes(WarningMinutesBefore);

        // Açık ve ilgilenilmesi gereken talepler — filtreleme SQL tarafında
        var tickets = await db.Tickets
            .Include(t => t.Assignee)
            .Where(t => t.ResolvedAt == null
                        && (t.Status == TicketStatus.New || t.Status == TicketStatus.InProgress)
                        && (t.SlaDueAt <= warnThreshold || t.IsSlaBreached))
            .ToListAsync(token);

        if (tickets.Count == 0) return;

        var rules = await db.EscalationRules
            .Where(r => r.IsActive)
            .OrderBy(r => r.Level)
            .ToListAsync(token);

        var changed = false;

        foreach (var t in tickets)
        {
            // 1) SLA yaklaşıyor uyarısı — talep başına bir kez
            if (!t.IsSlaBreached && t.SlaDueAt > now && t.SlaWarningSentAt == null)
            {
                var kalan = (int)(t.SlaDueAt - now).TotalMinutes;
                await NotifyOwnersAsync(db, notify, t, NotificationType.SlaWarning,
                    $"SLA yaklaşıyor: #{t.Id} {t.Title}",
                    $"Çözüm süresinin dolmasına {kalan} dakika kaldı.");

                t.SlaWarningSentAt = now;
                changed = true;
            }

            // 2) İhlal tespiti — bir kez işaretlenir
            if (!t.IsSlaBreached && now > t.SlaDueAt)
            {
                t.IsSlaBreached = true;
                t.SlaBreachedAt = now;
                t.IsEscalated = true;

                await NotifyOwnersAsync(db, notify, t, NotificationType.SlaBreach,
                    $"SLA ihlali: #{t.Id} {t.Title}",
                    "Çözüm süresi aşıldı, talep eskalasyona alındı.");

                changed = true;
            }

            // 3) Eskalasyon seviyeleri — gecikme büyüdükçe yükselir
            if (t.IsSlaBreached)
            {
                var overdue = (int)(now - t.SlaDueAt).TotalMinutes;

                foreach (var rule in rules.Where(r => r.Level > t.EscalationLevel && overdue >= r.AfterMinutes))
                {
                    db.EscalationRecords.Add(new EscalationRecord
                    {
                        TicketId = t.Id,
                        Level = rule.Level,
                        OccurredAt = now,
                        MinutesOverdue = overdue,
                        EscalatedToId = t.AssigneeId,
                        Note = $"{overdue} dakika gecikme, seviye {rule.Level} ({rule.TargetRole})"
                    });

                    await notify.QueueToRoleAsync(rule.TargetRole, NotificationType.Escalation,
                        $"Eskalasyon (seviye {rule.Level}): #{t.Id} {t.Title}",
                        $"Talep {overdue} dakikadır gecikmede. Atanan: {t.Assignee?.FullName ?? "yok"}.",
                        t.Id);

                    t.EscalationLevel = rule.Level;
                    changed = true;
                }
            }
        }

        if (changed) await db.SaveChangesAsync(token);
    }

    // Atanan kişi varsa ona, yoksa tüm destek ekibine
    private static async Task NotifyOwnersAsync(
        AppDbContext db, NotificationService notify, Ticket t,
        NotificationType type, string title, string message)
    {
        if (t.AssigneeId != null)
            notify.Queue(t.AssigneeId.Value, type, title, message, t.Id);
        else
            await notify.QueueToRoleAsync(UserRole.Agent, type, title, message, t.Id);
    }
}
