using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;
using ServiceDesk.Services.Messaging;

namespace ServiceDesk.Services.Notifications;

// Kuyruktaki e-posta ve SMS bildirimlerini gönderir. Yalnızca
// MessageQueueWorker tarafından çağrılır; istek içinde çalışmaz.
//
// Başarısız gönderim kaybolmaz: deneme sayısı artar, DeliveryRetryPolicy
// ile bir sonraki deneme zamanı planlanır. Sınır dolunca IsFailed = true.
public class NotificationDispatcher
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly MessagingSettings _cfg;
    private readonly ILogger<NotificationDispatcher> _log;

    public NotificationDispatcher(AppDbContext db, IEmailSender email, ISmsSender sms,
                                  IOptions<MessagingSettings> cfg, ILogger<NotificationDispatcher> log)
    {
        _db = db;
        _email = email;
        _sms = sms;
        _cfg = cfg.Value;
        _log = log;
    }

    // Zamanı gelmiş en fazla BatchSize kaydı işler; işlenen kayıt sayısını döner
    public async Task<int> DeliverDueAsync(CancellationToken token = default)
    {
        var now = AppTime.UtcNow;
        var q = _cfg.Queue;

        var due = await _db.Notifications
            .Include(n => n.Recipient)
            .Include(n => n.Ticket)
            .Where(n => n.Channel != NotificationChannel.InApp
                        && !n.IsSent && !n.IsFailed
                        && (n.NextAttemptAt == null || n.NextAttemptAt <= now))
            .OrderBy(n => n.NextAttemptAt).ThenBy(n => n.Id)
            .Take(Math.Clamp(q.BatchSize, 1, 500))
            .ToListAsync(token);

        if (due.Count == 0) return 0;

        foreach (var n in due)
        {
            token.ThrowIfCancellationRequested();
            n.AttemptCount++;
            n.LastAttemptAt = AppTime.UtcNow;

            var r = n.Recipient;
            if (r == null || !r.IsActive)
            {
                GiveUp(n, "Alıcı hesabı pasif veya silinmiş");
                continue;
            }

            try
            {
                switch (n.Channel)
                {
                    case NotificationChannel.Email:
                        if (string.IsNullOrWhiteSpace(r.Email)) { GiveUp(n, "Alıcının e-posta adresi yok"); continue; }
                        var mail = EmailTemplates.Render(
                            n.TemplateKey ?? EmailTemplates.DefaultKey(n.Type),
                            new EmailTemplates.Model(r.FullName, n.Title, n.Message,
                                n.Ticket?.Number ?? (n.TicketId == null ? null : $"#{n.TicketId}"),
                                TicketUrl(n, r)));
                        await _email.SendAsync(r.Email, mail.Subject, mail.Html, mail.Text);
                        break;

                    case NotificationChannel.Sms:
                        if (string.IsNullOrWhiteSpace(r.PhoneNumber)) { GiveUp(n, "Alıcının telefonu yok"); continue; }
                        var res = await _sms.SendAsync(PhoneRules.Normalize(r.PhoneNumber) ?? r.PhoneNumber,
                            EmailTemplates.Sms(n.Title, n.Ticket?.Number), token);
                        if (res.Status == OtpDeliveryStatus.Disabled) { GiveUp(n, "SMS kanalı kapalı"); continue; }
                        if (!res.Success) throw new InvalidOperationException(res.Error ?? "SMS gönderilemedi");
                        break;
                }

                n.IsSent = true;
                n.SentAt = AppTime.UtcNow;
                n.SendError = null;
                n.NextAttemptAt = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                n.SendError = Cut(ex.Message, 300);
                var outcome = DeliveryRetryPolicy.AfterFailure(n.AttemptCount, AppTime.UtcNow,
                    q.MaxAttempts, q.RetryBaseSeconds, q.RetryMaxSeconds);

                if (outcome.GiveUp)
                {
                    n.IsFailed = true;
                    n.NextAttemptAt = null;
                    _log.LogWarning("Bildirim #{Id} ({Channel}) {Attempts} denemeden sonra gönderilemedi: {Error}",
                        n.Id, n.Channel, n.AttemptCount, ex.Message);
                }
                else
                {
                    n.NextAttemptAt = outcome.NextAttemptAt;
                    _log.LogInformation("Bildirim #{Id} ({Channel}) gönderilemedi, {Next:O} zamanında yeniden denenecek",
                        n.Id, n.Channel, outcome.NextAttemptAt);
                }
            }
        }

        await _db.SaveChangesAsync(token);
        return due.Count;
    }

    // Yönetici ekranı: başarısız kaydı kuyruğa geri al
    public async Task<bool> RequeueAsync(int notificationId)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId
            && x.Channel != NotificationChannel.InApp && !x.IsSent);
        if (n == null) return false;

        n.IsFailed = false;
        n.AttemptCount = 0;
        n.NextAttemptAt = AppTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private static void GiveUp(Notification n, string reason)
    {
        n.IsFailed = true;
        n.SendError = reason;
        n.NextAttemptAt = null;
    }

    private string? TicketUrl(Notification n, AppUser r)
    {
        if (n.TicketId == null || string.IsNullOrWhiteSpace(_cfg.Email.PublicBaseUrl)) return null;
        var area = r.Role == UserRole.User ? "Portal" : "Tickets";
        return $"{_cfg.Email.PublicBaseUrl!.TrimEnd('/')}/{area}/Details/{n.TicketId}";
    }

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;
}
