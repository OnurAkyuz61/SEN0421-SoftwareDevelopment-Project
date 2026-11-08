using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using ServiceDesk.Services.Messaging;

namespace ServiceDesk.Services;

// E-posta gönderimi. Hangi uygulamanın kullanılacağı appsettings →
// Messaging:Email:Mode ile seçilir ("Logging" | "Smtp"), bkz. Program.cs.
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, string? plainText = null);
}

// Geliştirmede / SMTP yapılandırılmamışken gönderimi loga yazar.
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _log;

    public LoggingEmailSender(ILogger<LoggingEmailSender> log) => _log = log;

    public Task SendAsync(string to, string subject, string htmlBody, string? plainText = null)
    {
        _log.LogInformation(
            "E-posta (simülasyon) → {To}\nKonu: {Subject}\n{Body}",
            to, subject, plainText ?? htmlBody);
        return Task.CompletedTask;
    }
}

// Gerçek SMTP göndericisi (System.Net.Mail). HTML gövde ile birlikte düz
// metin alternatifi de eklenir; böylece HTML göstermeyen istemciler ve spam
// filtreleri için okunabilir bir sürüm olur.
//
// Hata durumunda istisna fırlatır: kuyruk worker'ı yakalayıp yeniden deneme
// planlar (DeliveryRetryPolicy).
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _cfg;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(IOptions<MessagingSettings> cfg, ILogger<SmtpEmailSender> log)
    {
        _cfg = cfg.Value.Email;
        _log = log;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, string? plainText = null)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_cfg.FromAddress, _cfg.FromName, Encoding.UTF8),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
            Body = plainText ?? StripTags(htmlBody)
        };
        message.To.Add(new MailAddress(to));
        message.AlternateViews.Add(
            AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html"));

        using var client = new SmtpClient(_cfg.Smtp.Host, _cfg.Smtp.Port)
        {
            EnableSsl = _cfg.Smtp.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = _cfg.Smtp.TimeoutSeconds * 1000
        };

        if (!string.IsNullOrWhiteSpace(_cfg.Smtp.UserName))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_cfg.Smtp.UserName, _cfg.Smtp.Password);
        }

        await client.SendMailAsync(message);
        _log.LogInformation("E-posta gönderildi → {To} ({Subject})", to, subject);
    }

    private static string StripTags(string html) =>
        WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ")).Trim();
}
