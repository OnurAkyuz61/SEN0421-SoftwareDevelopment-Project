namespace ServiceDesk.Services;

// Gerçek SMTP / SendGrid bağlanana kadar e-posta gönderimini simüle eder.
// Development'ta bağlantı TempData ile ekranda da gösterilir (OTP demo gibi).
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, string? plainText = null);
}

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
