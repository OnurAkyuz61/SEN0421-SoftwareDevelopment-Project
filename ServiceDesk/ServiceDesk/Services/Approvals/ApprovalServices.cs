using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Services.Approvals;

// Kararı verecek kişinin onay bağlamını (ApprovalContext) kurar: şu an
// geçerli vekaletler ve ayarlar. Controller ve view'lar ApprovalPolicy'yi
// bu bağlamla çağırır; böylece ekrandaki düğme ile sunucu kuralı aynı olur.
public class ApprovalAuthority
{
    private readonly AppDbContext _db;
    private readonly ApprovalSettings _cfg;

    public ApprovalAuthority(AppDbContext db, IOptions<ApprovalSettings> cfg)
    {
        _db = db;
        _cfg = cfg.Value;
    }

    public ApprovalSettings Settings => _cfg;

    public async Task<ApprovalContext> ContextForAsync(AppUser user)
    {
        var now = AppTime.UtcNow;
        var delegators = await _db.ApprovalDelegations.AsNoTracking()
            .Where(d => d.DelegateId == user.Id && d.RevokedAt == null && d.StartsAt <= now && now < d.EndsAt)
            .Select(d => d.Delegator!)
            .Where(u => u.IsActive && u.Role == UserRole.Admin)
            .ToListAsync();

        return new ApprovalContext(now, delegators, _cfg);
    }

    // Belirli bir yöneticinin şu an geçerli vekilleri (hatırlatma ve bildirim için)
    public Task<List<int>> ActiveDelegateIdsAsync(IEnumerable<int> delegatorIds)
    {
        var now = AppTime.UtcNow;
        var ids = delegatorIds.ToList();
        return _db.ApprovalDelegations.AsNoTracking()
            .Where(d => ids.Contains(d.DelegatorId) && d.RevokedAt == null && d.StartsAt <= now && now < d.EndsAt)
            .Select(d => d.DelegateId).Distinct().ToListAsync();
    }
}

// Talebin onay adımı sayısını zincir kurallarına göre çözer ve talebe yazar.
// Onay süreci başladıktan sonra çağrılmaz (adım sayısı tur boyunca sabittir).
public class ApprovalChainResolver
{
    private readonly AppDbContext _db;

    public ApprovalChainResolver(AppDbContext db) => _db = db;

    public async Task ApplyAsync(Ticket t)
    {
        if (t.Type != TicketType.Change) return;
        var rules = await _db.ApprovalChainRules.AsNoTracking().Where(r => r.IsActive).ToListAsync();
        t.ApprovalStepsRequired = ApprovalChainPolicy.Resolve(rules, t);
    }
}

// Onay bekleyen değişiklikler için hatırlatma job'ı. ReminderAfterHours'tan
// uzun süredir bekleyen talebin o adımda karar verebilecek kişilerine
// (yöneticiler, vekilleri; ara adımda destek ekibi) hatırlatma gider.
// Aynı talep için ReminderIntervalHours dolmadan tekrar gönderilmez.
public class ApprovalReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ApprovalSettings _cfg;
    private readonly ILogger<ApprovalReminderService> _log;

    public ApprovalReminderService(IServiceScopeFactory scopes, IOptions<ApprovalSettings> cfg,
                                   ILogger<ApprovalReminderService> log)
    {
        _scopes = scopes;
        _cfg = cfg.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _cfg.ReminderCheckMinutes)));
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (DbUpdateConcurrencyException)
            {
                _log.LogInformation("Onay hatırlatma turu eşzamanlı güncellemeye denk geldi; sonraki turda tekrar denenecek");
            }
            catch (Exception ex) { _log.LogError(ex, "Onay hatırlatma turu başarısız"); }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    // Hatırlatma zamanı geldi mi (saf; test edilebilir)
    public static bool IsDue(DateTime waitingSinceUtc, DateTime? lastReminderUtc, DateTime nowUtc,
                             int afterHours, int intervalHours) =>
        nowUtc - waitingSinceUtc >= TimeSpan.FromHours(afterHours)
        && (lastReminderUtc == null || nowUtc - lastReminderUtc.Value >= TimeSpan.FromHours(intervalHours));

    public async Task<int> RunOnceAsync(CancellationToken token = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notify = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var authority = scope.ServiceProvider.GetRequiredService<ApprovalAuthority>();

        var now = AppTime.UtcNow;
        var threshold = now.AddHours(-_cfg.ReminderAfterHours);

        var pending = await db.Tickets
            .Include(t => t.ChangeDetail)
            .Include(t => t.Approvals)
            .Where(t => t.ApprovalStatus == ApprovalStatus.Pending && t.Type == TicketType.Change
                        && t.CreatedAt <= threshold)
            .ToListAsync(token);

        var admins = await db.Users.Where(u => u.IsActive && u.Role == UserRole.Admin).Select(u => u.Id).ToListAsync(token);
        var delegates = await authority.ActiveDelegateIdsAsync(admins);

        var sent = 0;
        foreach (var t in pending)
        {
            // Bekleme, son karardan (ya da yeniden gönderimden) itibaren sayılır
            var since = t.CurrentRoundApprovals.Select(a => (DateTime?)a.DecidedAt).Max()
                        ?? t.ApprovalDecidedAt ?? t.CreatedAt;
            if (t.ApprovalRound > 1 && t.ApprovalDecidedAt is { } resubmitted && resubmitted > since) since = resubmitted;
            if (!IsDue(since, t.LastApprovalReminderAt, now, _cfg.ReminderAfterHours, _cfg.ReminderIntervalHours))
                continue;

            var step = ApprovalPolicy.NextStep(t);
            var final = ApprovalPolicy.IsFinalStep(t);
            var waitedHours = (int)(now - since).TotalHours;

            // Nihai adım: yöneticiler + vekilleri; ara adım: destek + yöneticiler
            var recipients = new HashSet<int>(admins);
            recipients.UnionWith(delegates);
            if (!final)
                recipients.UnionWith(await db.Users.Where(u => u.IsActive && u.Role == UserRole.Agent)
                    .Select(u => u.Id).ToListAsync(token));

            // Talep sahibi ve bu turda zaten onay vermiş olanlar hatırlatılmaz
            recipients.Remove(t.RequesterId);
            foreach (var a in t.CurrentRoundApprovals) recipients.Remove(a.ApproverId);

            var window = t.ChangeDetail?.PlannedStart is { } ps
                ? $" Planlanan başlangıç: {AppTime.ToLocal(ps):dd.MM.yyyy HH:mm}."
                : "";

            foreach (var id in recipients)
                notify.Queue(id, NotificationType.ApprovalReminder,
                    $"Onay hatırlatma: {Ui.TicketNo(t)} {t.Title}",
                    $"{waitedHours} saattir {step}/{t.RequiredApprovalSteps}. adım kararı bekleniyor.{window}",
                    t.Id, EmailTemplates.ApprovalReminder);

            t.LastApprovalReminderAt = now;
            sent++;
        }

        if (sent > 0)
        {
            await db.SaveChangesAsync(token);
            notify.SignalDelivery();
            _log.LogInformation("Onay hatırlatma: {Count} talep için gönderildi", sent);
        }
        return sent;
    }
}
