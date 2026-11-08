using Microsoft.Extensions.Options;
using ServiceDesk.Models;

namespace ServiceDesk.Services.Messaging;

// Sağlayıcıdan dönen sonuç. Başarılıysa sağlayıcının mesaj numarası,
// değilse hata metni taşınır; istisna fırlatmak yerine sonuç döner ki
// çağıran (OTP akışı, kuyruk worker'ı) gönderimi kaydedebilsin.
public sealed record SendResult(
    bool Success,
    string Provider,
    string? ProviderMessageId = null,
    string? Error = null,
    OtpDeliveryStatus Status = OtpDeliveryStatus.Sent)
{
    public static SendResult Disabled(string provider) =>
        new(false, provider, Error: "Kanal yapılandırmada kapalı", Status: OtpDeliveryStatus.Disabled);
}

// ── SMS ──────────────────────────────────────────────────────────────────

public interface ISmsSender
{
    bool IsEnabled { get; }
    Task<SendResult> SendAsync(string phoneE164, string message, CancellationToken token = default);
}

// Gerçek SMS sağlayıcısı bağlanana kadar gönderimi loglar. Telefonun tamamı
// ve mesaj metni (OTP kodunu içerir) yalnızca Development'ta yazılır;
// diğer ortamlarda maskelenir.
public class LoggingSmsSender : ISmsSender
{
    private readonly ILogger<LoggingSmsSender> _log;
    private readonly SmsSettings _cfg;
    private readonly IHostEnvironment _env;

    public LoggingSmsSender(ILogger<LoggingSmsSender> log, IOptions<MessagingSettings> cfg, IHostEnvironment env)
    {
        _log = log;
        _cfg = cfg.Value.Sms;
        _env = env;
    }

    public bool IsEnabled => _cfg.Enabled;

    public Task<SendResult> SendAsync(string phoneE164, string message, CancellationToken token = default)
    {
        if (!_cfg.Enabled) return Task.FromResult(SendResult.Disabled("Logging-SMS"));

        var id = $"sms-{Guid.NewGuid():N}"[..20];
        if (_env.IsDevelopment())
            _log.LogInformation("SMS (stub) [{Sender}] → {Phone} #{Id}: {Message}", _cfg.SenderId, phoneE164, id, message);
        else
            _log.LogInformation("SMS (stub) [{Sender}] → {Phone} #{Id} ({Length} karakter)",
                _cfg.SenderId, PhoneMask.Mask(phoneE164), id, message.Length);

        // Stub teslimi anında "teslim edildi" kabul eder; gerçek sağlayıcıda
        // durum webhook / sorgu ile güncellenir (OtpDeliveryService.UpdateStatusAsync)
        return Task.FromResult(new SendResult(true, "Logging-SMS", id, Status: OtpDeliveryStatus.Delivered));
    }
}

// ── WhatsApp ─────────────────────────────────────────────────────────────

public interface IWhatsAppSender
{
    bool IsEnabled { get; }

    // OTP için onaylı şablon mesajı (WhatsApp serbest metinle oturum açmaya izin vermez)
    Task<SendResult> SendOtpAsync(string phoneE164, string code, int minutes, CancellationToken token = default);

    // Bildirimler için serbest metin (24 saatlik oturum içinde)
    Task<SendResult> SendTextAsync(string phoneE164, string message, CancellationToken token = default);
}

public class LoggingWhatsAppSender : IWhatsAppSender
{
    private readonly ILogger<LoggingWhatsAppSender> _log;
    private readonly WhatsAppSettings _cfg;
    private readonly IHostEnvironment _env;

    public LoggingWhatsAppSender(ILogger<LoggingWhatsAppSender> log, IOptions<MessagingSettings> cfg, IHostEnvironment env)
    {
        _log = log;
        _cfg = cfg.Value.WhatsApp;
        _env = env;
    }

    public bool IsEnabled => _cfg.Enabled;

    public Task<SendResult> SendOtpAsync(string phoneE164, string code, int minutes, CancellationToken token = default)
    {
        if (!_cfg.Enabled) return Task.FromResult(SendResult.Disabled("Logging-WhatsApp"));

        var id = $"wamid-{Guid.NewGuid():N}"[..22];
        if (_env.IsDevelopment())
            _log.LogInformation("WhatsApp (stub) şablon={Template}/{Lang} → {Phone} #{Id}: kod {Code}, {Minutes} dk",
                _cfg.OtpTemplateName, _cfg.LanguageCode, phoneE164, id, code, minutes);
        else
            _log.LogInformation("WhatsApp (stub) şablon={Template} → {Phone} #{Id}",
                _cfg.OtpTemplateName, PhoneMask.Mask(phoneE164), id);

        return Task.FromResult(new SendResult(true, "Logging-WhatsApp", id, Status: OtpDeliveryStatus.Delivered));
    }

    public Task<SendResult> SendTextAsync(string phoneE164, string message, CancellationToken token = default)
    {
        if (!_cfg.Enabled) return Task.FromResult(SendResult.Disabled("Logging-WhatsApp"));

        var id = $"wamid-{Guid.NewGuid():N}"[..22];
        _log.LogInformation("WhatsApp (stub) → {Phone} #{Id}: {Message}",
            _env.IsDevelopment() ? phoneE164 : PhoneMask.Mask(phoneE164), id, message);
        return Task.FromResult(new SendResult(true, "Logging-WhatsApp", id));
    }
}

public static class PhoneMask
{
    public static string Mask(string? phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 7) return "***";
        return $"{digits[..4]} *** ** {digits[^2..]}";
    }
}
