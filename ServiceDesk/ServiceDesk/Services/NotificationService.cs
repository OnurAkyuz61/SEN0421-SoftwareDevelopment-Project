using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Bildirimler artık talep verisinden anlık hesaplanmıyor; Notifications
// tablosunda duruyor. Bu servis hem yazma (olay anında) hem okuma tarafını yönetir.
// Uygulama içi kaydın yanında, kritik olaylar için e-posta kuyruğu da açılır;
// SMTP bağlanana kadar LoggingEmailSender konsola yazar.
public class NotificationService
{
    // Bu türler için uygulama içi + e-posta birlikte kuyruğa alınır
    private static readonly HashSet<NotificationType> EmailTypes =
    [
        NotificationType.NewTicket,
        NotificationType.ApprovalPending,
        NotificationType.ApprovalDecided,
        NotificationType.SlaWarning,
        NotificationType.SlaBreach,
        NotificationType.Escalation,
        NotificationType.Assigned
    ];

    private readonly AppDbContext _db;
    private readonly IEmailSender _email;

    public NotificationService(AppDbContext db, IEmailSender email)
    {
        _db = db;
        _email = email;
    }

    // ── Okuma ────────────────────────────────────────────────────────────

    public Task<List<Notification>> GetForAsync(int userId, bool unreadOnly = false, int take = 100)
    {
        var q = _db.Notifications
            .AsNoTracking()
            .Include(n => n.Ticket)
            .Where(n => n.RecipientId == userId && n.Channel == NotificationChannel.InApp);

        if (unreadOnly) q = q.Where(n => n.ReadAt == null);

        return q.OrderByDescending(n => n.CreatedAt).Take(take).ToListAsync();
    }

    public Task<int> UnreadCountAsync(int userId) =>
        _db.Notifications.CountAsync(n => n.RecipientId == userId
            && n.Channel == NotificationChannel.InApp && n.ReadAt == null);

    public async Task MarkReadAsync(int userId, int? notificationId = null)
    {
        var q = _db.Notifications.Where(n => n.RecipientId == userId
            && n.Channel == NotificationChannel.InApp && n.ReadAt == null);
        if (notificationId != null) q = q.Where(n => n.Id == notificationId);

        foreach (var n in await q.ToListAsync()) n.ReadAt = AppTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Yazma ────────────────────────────────────────────────────────────

    // Kaydı ekler ama SaveChanges çağırmaz: çağıran kendi işlemiyle birlikte kaydeder
    public void Queue(int recipientId, NotificationType type, string title, string message, int? ticketId = null)
    {
        _db.Notifications.Add(new Notification
        {
            RecipientId = recipientId,
            TicketId = ticketId,
            Type = type,
            Channel = NotificationChannel.InApp,
            Title = title,
            Message = message,
            IsSent = true,
            SentAt = AppTime.UtcNow
        });

        if (EmailTypes.Contains(type))
        {
            _db.Notifications.Add(new Notification
            {
                RecipientId = recipientId,
                TicketId = ticketId,
                Type = type,
                Channel = NotificationChannel.Email,
                Title = title,
                Message = message,
                IsSent = false,
                SentAt = null
            });
        }
    }

    // Belirli roldeki aktif kullanıcıların tümüne
    public async Task QueueToRoleAsync(UserRole role, NotificationType type, string title, string message, int? ticketId = null, int? exceptUserId = null)
    {
        var ids = await _db.Users
            .Where(u => u.Role == role && u.IsActive && (exceptUserId == null || u.Id != exceptUserId))
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var id in ids) Queue(id, type, title, message, ticketId);
    }

    // SaveChanges sonrası çağrılır: bekleyen e-posta bildirimlerini gönderir
    public async Task DeliverPendingEmailsAsync()
    {
        var pending = await _db.Notifications
            .Include(n => n.Recipient)
            .Where(n => n.Channel == NotificationChannel.Email && !n.IsSent)
            .OrderBy(n => n.CreatedAt)
            .Take(100)
            .ToListAsync();

        if (pending.Count == 0) return;

        foreach (var n in pending)
        {
            var to = n.Recipient?.Email;
            if (string.IsNullOrWhiteSpace(to) || n.Recipient?.IsActive != true)
            {
                n.IsSent = true;
                n.SentAt = AppTime.UtcNow;
                n.SendError = "Alıcı e-postası yok veya hesap pasif";
                continue;
            }

            try
            {
                var html =
                    $"<p><strong>{System.Net.WebUtility.HtmlEncode(n.Title)}</strong></p>" +
                    $"<p>{System.Net.WebUtility.HtmlEncode(n.Message)}</p>" +
                    (n.TicketId != null
                        ? $"<p>Talep no: #{n.TicketId}</p>"
                        : "") +
                    "<p style=\"color:#666;font-size:12px\">ServiceDesk · ISO 9001 IT Servis Masası</p>";

                await _email.SendAsync(to, $"ServiceDesk · {n.Title}", html, $"{n.Title}\n\n{n.Message}");
                n.IsSent = true;
                n.SentAt = AppTime.UtcNow;
                n.SendError = null;
            }
            catch (Exception ex)
            {
                n.SendError = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message;
            }
        }

        await _db.SaveChangesAsync();
    }

    public static string Describe(NotificationType type) => type switch
    {
        NotificationType.SlaWarning => "bi-hourglass-split",
        NotificationType.SlaBreach => "bi-exclamation-octagon",
        NotificationType.Escalation => "bi-arrow-up-circle",
        NotificationType.Assigned => "bi-person-check",
        NotificationType.ApprovalPending => "bi-patch-question",
        NotificationType.ApprovalDecided => "bi-check2-square",
        NotificationType.NewTicket => "bi-plus-circle",
        _ => "bi-chat-left-text"
    };

    public static string Color(NotificationType type) => type switch
    {
        NotificationType.SlaBreach or NotificationType.Escalation => "danger",
        NotificationType.SlaWarning or NotificationType.ApprovalPending => "warning",
        NotificationType.NewTicket => "success",
        _ => "primary"
    };
}
