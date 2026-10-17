using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Bildirimler artık talep verisinden anlık hesaplanmıyor; Notifications
// tablosunda duruyor. Bu servis hem yazma (olay anında) hem okuma tarafını yönetir.
public class NotificationService
{
    private readonly AppDbContext _db;
    public NotificationService(AppDbContext db) => _db = db;

    // ── Okuma ────────────────────────────────────────────────────────────

    public Task<List<Notification>> GetForAsync(int userId, bool unreadOnly = false, int take = 100)
    {
        var q = _db.Notifications
            .AsNoTracking()
            .Include(n => n.Ticket)
            .Where(n => n.RecipientId == userId);

        if (unreadOnly) q = q.Where(n => n.ReadAt == null);

        return q.OrderByDescending(n => n.CreatedAt).Take(take).ToListAsync();
    }

    public Task<int> UnreadCountAsync(int userId) =>
        _db.Notifications.CountAsync(n => n.RecipientId == userId && n.ReadAt == null);

    public async Task MarkReadAsync(int userId, int? notificationId = null)
    {
        var q = _db.Notifications.Where(n => n.RecipientId == userId && n.ReadAt == null);
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
            Title = title,
            Message = message
        });
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

    public static string Describe(NotificationType type) => type switch
    {
        NotificationType.SlaWarning => "bi-hourglass-split",
        NotificationType.SlaBreach => "bi-exclamation-octagon",
        NotificationType.Escalation => "bi-arrow-up-circle",
        NotificationType.Assigned => "bi-person-check",
        NotificationType.ApprovalPending => "bi-patch-question",
        NotificationType.ApprovalDecided => "bi-check2-square",
        _ => "bi-chat-left-text"
    };

    public static string Color(NotificationType type) => type switch
    {
        NotificationType.SlaBreach or NotificationType.Escalation => "danger",
        NotificationType.SlaWarning or NotificationType.ApprovalPending => "warning",
        _ => "primary"
    };
}
