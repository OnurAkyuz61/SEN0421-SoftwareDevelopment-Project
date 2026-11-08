using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Services;

// Bildirimlerin yazma (olay anında) ve okuma tarafı.
//
// Yazma: Queue çağrısı, alıcının tür bazlı tercihine göre (NotificationChannelPolicy)
// uygulama içi, e-posta ve SMS kayıtlarını ekler. E-posta / SMS kayıtları
// "gönderilmemiş" olarak kuyruğa girer; gönderimi bu servis YAPMAZ, ayrı
// çalışan MessageQueueWorker yapar. Controller'lar kayıttan sonra yalnızca
// SignalDelivery() ile worker'ı uyandırır, istek gönderimi beklemez.
public class NotificationService
{
    private const int MarkReadBatchSize = 500;

    private readonly AppDbContext _db;
    private readonly UnreadCountCache _unread;
    private readonly MessageQueueSignal _signal;

    // Aynı istek içinde aynı alıcıya birden çok bildirim gidebilir; tercih ve
    // iletişim bilgisi bir kez okunur
    private readonly Dictionary<int, RecipientInfo?> _recipients = new();

    private sealed record RecipientInfo(bool HasEmail, bool HasPhone,
        Dictionary<NotificationType, NotificationPreference> Prefs);

    public NotificationService(AppDbContext db, UnreadCountCache unread, MessageQueueSignal signal)
    {
        _db = db;
        _unread = unread;
        _signal = signal;
    }

    // ── Okuma ────────────────────────────────────────────────────────────

    private IQueryable<Notification> InAppFor(int userId, bool unreadOnly)
    {
        var q = _db.Notifications
            .Where(n => n.RecipientId == userId && n.Channel == NotificationChannel.InApp);
        return unreadOnly ? q.Where(n => n.ReadAt == null) : q;
    }

    public Task<List<Notification>> GetForAsync(int userId, bool unreadOnly = false, int take = 100) =>
        InAppFor(userId, unreadOnly)
            .AsNoTracking()
            .Include(n => n.Ticket)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();

    // Bildirim listesi sayfalı okunur
    public Task<(List<Notification> Items, PagerInfo Pager)> GetPageAsync(
        int userId, bool unreadOnly, int page, int pageSize = Paging.DefaultPageSize) =>
        InAppFor(userId, unreadOnly)
            .AsNoTracking()
            .Include(n => n.Ticket)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .ToPageAsync(page, pageSize);

    // Tek bildirim: alıcı filtresi sorgunun içinde; başkasının bildirimi dönmez
    public Task<Notification?> GetOwnAsync(int userId, int notificationId) =>
        _db.Notifications
            .AsNoTracking()
            .Include(n => n.Ticket)
            .FirstOrDefaultAsync(n => n.Id == notificationId
                                      && n.RecipientId == userId
                                      && n.Channel == NotificationChannel.InApp);

    // Önbellekli sayaç (UnreadCountCache); zil ve /Notifications/UnreadCount kullanır
    public Task<int> UnreadCountAsync(int userId) =>
        _unread.GetOrAddAsync(userId, () => InAppFor(userId, unreadOnly: true).CountAsync());

    // Tek bildirimi okundu işaretler
    public async Task MarkReadAsync(int userId, int? notificationId = null)
    {
        if (notificationId == null)
        {
            await MarkAllReadAsync(userId);
            return;
        }

        var n = await InAppFor(userId, unreadOnly: true).FirstOrDefaultAsync(x => x.Id == notificationId);
        if (n == null) return;
        n.ReadAt = AppTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // Ekranda görünen sayfadaki bildirimler (yalnızca kullanıcının kendi kayıtları)
    public async Task<int> MarkReadAsync(int userId, IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0) return 0;
        var safeIds = ids.Take(Paging.MaxPageSize).ToList();

        var count = await InAppFor(userId, unreadOnly: true)
            .Where(n => safeIds.Contains(n.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, AppTime.UtcNow));

        _unread.Invalidate(userId);
        return count;
    }

    // "Tümünü okundu say": binlerce bildirim tek seferde belleğe çekilip
    // tek tek güncellenmez; 500'lük sayfalar halinde doğrudan SQL UPDATE ile işlenir.
    public async Task<int> MarkAllReadAsync(int userId)
    {
        var total = 0;
        var now = AppTime.UtcNow;

        while (true)
        {
            var batch = await InAppFor(userId, unreadOnly: true)
                .OrderBy(n => n.Id)
                .Select(n => n.Id)
                .Take(MarkReadBatchSize)
                .ToListAsync();
            if (batch.Count == 0) break;

            total += await _db.Notifications
                .Where(n => batch.Contains(n.Id) && n.ReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now));

            if (batch.Count < MarkReadBatchSize) break;
        }

        _unread.Invalidate(userId);
        return total;
    }

    // ── Tercihler ────────────────────────────────────────────────────────

    // Düzenlenebilir her tür için geçerli tercih (kayıt yoksa varsayılan)
    public async Task<List<NotificationPreference>> GetPreferencesAsync(int userId)
    {
        var saved = await _db.NotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => p.Type);

        return NotificationChannelPolicy.ConfigurableTypes
            .Select(t => saved.TryGetValue(t, out var p)
                ? p
                : new NotificationPreference
                {
                    UserId = userId,
                    Type = t,
                    InApp = NotificationChannelPolicy.Default(t).InApp,
                    Email = NotificationChannelPolicy.Default(t).Email,
                    Sms = NotificationChannelPolicy.Default(t).Sms
                })
            .ToList();
    }

    public async Task SavePreferencesAsync(int userId, IEnumerable<NotificationPreference> prefs)
    {
        var existing = await _db.NotificationPreferences
            .Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => p.Type);

        foreach (var p in prefs)
        {
            if (!NotificationChannelPolicy.ConfigurableTypes.Contains(p.Type)) continue;
            var rule = NotificationChannelPolicy.RuleFor(p.Type);

            if (!existing.TryGetValue(p.Type, out var row))
            {
                row = new NotificationPreference { UserId = userId, Type = p.Type };
                _db.NotificationPreferences.Add(row);
            }

            row.InApp = rule.InAppLocked || p.InApp;
            row.Email = p.Email;
            row.Sms = rule.SmsAllowed && p.Sms;
            row.UpdatedAt = AppTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        _recipients.Remove(userId);
    }

    // ── Yazma ────────────────────────────────────────────────────────────

    // Kaydı ekler ama SaveChanges çağırmaz: çağıran kendi işlemiyle birlikte kaydeder.
    // templateKey: e-posta şablonu (EmailTemplates); boşsa türün varsayılanı.
    public void Queue(int recipientId, NotificationType type, string title, string message,
                      int? ticketId = null, string? templateKey = null)
    {
        var info = Recipient(recipientId);
        if (info == null) return; // pasif / silinmiş hesap

        info.Prefs.TryGetValue(type, out var pref);
        var ch = NotificationChannelPolicy.Resolve(type, pref, info.HasEmail, info.HasPhone);

        title = Cut(title, 200);
        message = Cut(message, 500);
        var now = AppTime.UtcNow;

        if (ch.InApp)
            _db.Notifications.Add(new Notification
            {
                RecipientId = recipientId, TicketId = ticketId, Type = type,
                Channel = NotificationChannel.InApp, Title = title, Message = message,
                IsSent = true, SentAt = now, CreatedAt = now
            });

        if (ch.Email)
            _db.Notifications.Add(Pending(recipientId, ticketId, type, NotificationChannel.Email,
                title, message, templateKey ?? EmailTemplates.DefaultKey(type), now));

        if (ch.Sms)
            _db.Notifications.Add(Pending(recipientId, ticketId, type, NotificationChannel.Sms,
                title, message, templateKey, now));
    }

    // Tercihten bağımsız doğrudan e-posta kuyruğu (ör. günlük özet)
    public void QueueEmail(int recipientId, NotificationType type, string title, string message,
                           int? ticketId = null, string? templateKey = null) =>
        _db.Notifications.Add(Pending(recipientId, ticketId, type, NotificationChannel.Email,
            Cut(title, 200), Cut(message, 500), templateKey ?? EmailTemplates.DefaultKey(type), AppTime.UtcNow));

    // Belirli roldeki aktif kullanıcıların tümüne
    public async Task QueueToRoleAsync(UserRole role, NotificationType type, string title, string message,
                                       int? ticketId = null, int? exceptUserId = null, string? templateKey = null)
    {
        var ids = await _db.Users
            .Where(u => u.Role == role && u.IsActive && (exceptUserId == null || u.Id != exceptUserId))
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var id in ids) Queue(id, type, title, message, ticketId, templateKey);
    }

    // Kayıttan sonra çağrılır: kuyruk worker'ını beklemeden uyandırır.
    // Gönderim istek içinde YAPILMAZ (MessageQueueWorker).
    public void SignalDelivery() => _signal.Notify();

    private static Notification Pending(int recipientId, int? ticketId, NotificationType type,
        NotificationChannel channel, string title, string message, string? templateKey, DateTime now) => new()
    {
        RecipientId = recipientId, TicketId = ticketId, Type = type, Channel = channel,
        Title = title, Message = message, TemplateKey = templateKey,
        IsSent = false, SentAt = null, CreatedAt = now, NextAttemptAt = now
    };

    private RecipientInfo? Recipient(int userId)
    {
        if (_recipients.TryGetValue(userId, out var cached)) return cached;

        var u = _db.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive)
            .Select(x => new { x.Email, x.PhoneNumber })
            .FirstOrDefault();

        RecipientInfo? info = null;
        if (u != null)
        {
            var prefs = _db.NotificationPreferences.AsNoTracking()
                .Where(p => p.UserId == userId)
                .ToDictionary(p => p.Type);
            info = new RecipientInfo(!string.IsNullOrWhiteSpace(u.Email),
                                     !string.IsNullOrWhiteSpace(u.PhoneNumber), prefs);
        }

        _recipients[userId] = info;
        return info;
    }

    private static string Cut(string s, int max) => s.Length > max ? s[..(max - 1)] + "…" : s;

    public static string Describe(NotificationType type) => type switch
    {
        NotificationType.SlaWarning => "bi-hourglass-split",
        NotificationType.SlaBreach => "bi-exclamation-octagon",
        NotificationType.Escalation => "bi-arrow-up-circle",
        NotificationType.Assigned => "bi-person-check",
        NotificationType.ApprovalPending => "bi-patch-question",
        NotificationType.ApprovalDecided => "bi-check2-square",
        NotificationType.ApprovalReminder => "bi-alarm",
        NotificationType.ChangeImplementation => "bi-tools",
        NotificationType.DailyDigest => "bi-card-list",
        NotificationType.NewTicket => "bi-plus-circle",
        _ => "bi-chat-left-text"
    };

    public static string Color(NotificationType type) => type switch
    {
        NotificationType.SlaBreach or NotificationType.Escalation => "danger",
        NotificationType.SlaWarning or NotificationType.ApprovalPending or NotificationType.ApprovalReminder => "warning",
        NotificationType.NewTicket => "success",
        _ => "primary"
    };

    public static string TypeText(NotificationType type) => type switch
    {
        NotificationType.SlaWarning => "SLA yaklaşıyor",
        NotificationType.SlaBreach => "SLA ihlali",
        NotificationType.Escalation => "Eskalasyon",
        NotificationType.Assigned => "Talep atandı",
        NotificationType.ApprovalPending => "Onay bekliyor",
        NotificationType.ApprovalDecided => "Onay kararı",
        NotificationType.NewComment => "Yeni mesaj",
        NotificationType.NewTicket => "Yeni talep",
        NotificationType.DailyDigest => "Günlük özet",
        NotificationType.ApprovalReminder => "Onay hatırlatma",
        NotificationType.ChangeImplementation => "Değişiklik uygulaması",
        _ => type.ToString()
    };
}
