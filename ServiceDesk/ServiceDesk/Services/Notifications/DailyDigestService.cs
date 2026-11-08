using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services.Notifications;

public class NotificationSettings
{
    public const string Section = "Notifications";

    // Günlük özet açık mı ve yerel saatle kaçta gönderilsin
    public bool DigestEnabled { get; set; } = true;
    public int DigestHourLocal { get; set; } = 8;

    // Okunmamış sayaç API'si için istemci yoklama aralığı önerisi (sn)
    public int UnreadPollSeconds { get; set; } = 60;
}

// Günlük özet e-posta job'ı. Her gün DigestHourLocal'dan sonra, özeti açık
// olan her aktif kullanıcıya bir kez çalışır. Son gönderim AppUser.LastDigestSentAt
// alanında tutulduğu için uygulama yeniden başlasa da aynı gün iki kez gitmez.
public class DailyDigestService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopes;
    private readonly NotificationSettings _cfg;
    private readonly ILogger<DailyDigestService> _log;

    public DailyDigestService(IServiceScopeFactory scopes, IOptions<NotificationSettings> cfg,
                              ILogger<DailyDigestService> log)
    {
        _scopes = scopes;
        _cfg = cfg.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cfg.DigestEnabled) return;

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Günlük özet hazırlanamadı");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    // Bugünün özet anı (UTC). Henüz o saate gelinmediyse null.
    public static DateTime? TodaysSlotUtc(DateTime nowUtc, int hourLocal)
    {
        var local = AppTime.ToLocal(nowUtc);
        var slotLocal = local.Date.AddHours(Math.Clamp(hourLocal, 0, 23));
        return local < slotLocal ? null : AppTime.ToUtc(slotLocal);
    }

    public async Task<int> RunOnceAsync(CancellationToken token = default)
    {
        var now = AppTime.UtcNow;
        if (TodaysSlotUtc(now, _cfg.DigestHourLocal) is not { } slot) return 0;

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notify = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var users = await db.Users
            .Where(u => u.IsActive && u.DailyDigestEnabled && u.Email != ""
                        && (u.LastDigestSentAt == null || u.LastDigestSentAt < slot))
            .ToListAsync(token);

        var sent = 0;
        var since = now.AddHours(-24);

        foreach (var u in users)
        {
            var body = await BuildAsync(db, u, since, token);
            if (body != null)
            {
                notify.QueueEmail(u.Id, NotificationType.DailyDigest,
                    $"Günlük özet · {AppTime.ToLocal(now):dd.MM.yyyy}", body, templateKey: EmailTemplates.Digest);
                sent++;
            }
            // Bildirilecek bir şey olmasa da bugün için işaretlenir
            u.LastDigestSentAt = now;
        }

        await db.SaveChangesAsync(token);
        if (sent > 0) notify.SignalDelivery();
        _log.LogInformation("Günlük özet: {Count} kullanıcıya kuyruğa alındı", sent);
        return sent;
    }

    // Özet metni; anlatacak bir şey yoksa null (boş e-posta gönderilmez)
    private static async Task<string?> BuildAsync(AppDbContext db, AppUser u, DateTime since, CancellationToken token)
    {
        var sb = new StringBuilder();

        var unread = await db.Notifications.CountAsync(n => n.RecipientId == u.Id
            && n.Channel == NotificationChannel.InApp && n.ReadAt == null, token);
        var newNotifs = await db.Notifications.CountAsync(n => n.RecipientId == u.Id
            && n.Channel == NotificationChannel.InApp && n.CreatedAt >= since, token);

        if (u.Role == UserRole.User)
        {
            var mine = await db.Tickets.AsNoTracking()
                .Where(t => t.RequesterId == u.Id && (t.Status == TicketStatus.New || t.Status == TicketStatus.InProgress
                            || t.Status == TicketStatus.Resolved))
                .OrderBy(t => t.CreatedAt).Take(10)
                .Select(t => new { t.Number, t.Id, t.Title, t.Status })
                .ToListAsync(token);

            if (mine.Count == 0 && newNotifs == 0) return null;

            sb.AppendLine($"Açık talepleriniz: {mine.Count}");
            foreach (var t in mine)
                sb.AppendLine($"• {t.Number ?? "#" + t.Id} {t.Title} — {Ui.StatusText(t.Status)}");
        }
        else
        {
            var open = db.Tickets.AsNoTracking()
                .Where(t => t.AssigneeId == u.Id && (t.Status == TicketStatus.New || t.Status == TicketStatus.InProgress));

            var openCount = await open.CountAsync(token);
            var breached = await open.CountAsync(t => t.IsSlaBreached, token);
            var soon = await open.OrderBy(t => t.SlaDueAt).Take(5)
                .Select(t => new { t.Number, t.Id, t.Title, t.SlaDueAt, t.IsSlaBreached })
                .ToListAsync(token);

            var pendingApprovals = u.Role == UserRole.Admin
                ? await db.Tickets.CountAsync(t => t.ApprovalStatus == ApprovalStatus.Pending, token)
                : 0;
            var escalations = await db.EscalationRecords.CountAsync(e => e.OccurredAt >= since
                && (e.EscalatedToId == u.Id || e.Ticket!.AssigneeId == u.Id), token);

            if (openCount == 0 && newNotifs == 0 && pendingApprovals == 0) return null;

            sb.AppendLine($"Üzerinizdeki açık talep: {openCount} (SLA ihlalli: {breached})");
            if (pendingApprovals > 0) sb.AppendLine($"Onay bekleyen değişiklik: {pendingApprovals}");
            if (escalations > 0) sb.AppendLine($"Son 24 saatte eskalasyon: {escalations}");
            if (soon.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("SLA sırasına göre ilk talepler:");
                foreach (var t in soon)
                    sb.AppendLine($"• {t.Number ?? "#" + t.Id} {t.Title} — hedef {AppTime.ToLocal(t.SlaDueAt):dd.MM HH:mm}{(t.IsSlaBreached ? " (İHLAL)" : "")}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Son 24 saatte {newNotifs} yeni bildirim, toplam {unread} okunmamış.");
        return sb.ToString().TrimEnd();
    }
}
