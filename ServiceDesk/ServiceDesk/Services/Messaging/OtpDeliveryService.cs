using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services.Messaging;

// OTP kodunu seçilen kanaldan (SMS / WhatsApp) gönderir ve her denemeyi
// OtpDeliveryLogs tablosuna yazar. Gönderim başarısızsa denetim kaydı da
// düşülür. Kodun kendisi hiçbir tabloya yazılmaz.
public class OtpDeliveryService
{
    private readonly AppDbContext _db;
    private readonly ISmsSender _sms;
    private readonly IWhatsAppSender _whatsApp;
    private readonly AuditService _audit;
    private readonly IHttpContextAccessor _http;
    private readonly MessagingSettings _cfg;

    public OtpDeliveryService(AppDbContext db, ISmsSender sms, IWhatsAppSender whatsApp,
                              AuditService audit, IHttpContextAccessor http,
                              IOptions<MessagingSettings> cfg)
    {
        _db = db;
        _sms = sms;
        _whatsApp = whatsApp;
        _audit = audit;
        _http = http;
        _cfg = cfg.Value;
    }

    public bool IsChannelEnabled(OtpDeliveryChannel channel) => channel switch
    {
        OtpDeliveryChannel.Sms => _sms.IsEnabled,
        OtpDeliveryChannel.WhatsApp => _whatsApp.IsEnabled,
        _ => false
    };

    public IReadOnlyList<OtpDeliveryChannel> EnabledChannels() =>
        Enum.GetValues<OtpDeliveryChannel>().Where(IsChannelEnabled).ToList();

    public async Task<OtpDeliveryLog> SendAsync(int userId, string email, string phone,
        OtpDeliveryChannel channel, string code, int lifetimeSeconds, int sendNumber,
        CancellationToken token = default)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetimeSeconds / 60.0));
        var e164 = PhoneRules.Normalize(phone) ?? phone;

        SendResult result;
        try
        {
            result = channel switch
            {
                OtpDeliveryChannel.WhatsApp => await _whatsApp.SendOtpAsync(e164, code, minutes, token),
                _ => await _sms.SendAsync(e164,
                        _cfg.Sms.OtpTemplate.Replace("{code}", code).Replace("{minutes}", minutes.ToString()),
                        token)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = new SendResult(false, channel.ToString(), Error: ex.Message, Status: OtpDeliveryStatus.Failed);
        }

        var log = new OtpDeliveryLog
        {
            UserId = userId,
            Channel = channel,
            MaskedPhone = PhoneMask.Mask(phone),
            Status = result.Success ? result.Status : (result.Status == OtpDeliveryStatus.Disabled
                ? OtpDeliveryStatus.Disabled : OtpDeliveryStatus.Failed),
            Provider = result.Provider,
            ProviderMessageId = result.ProviderMessageId,
            Error = Trim(result.Error, 300),
            IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            SendNumber = sendNumber,
            CreatedAt = AppTime.UtcNow,
            StatusUpdatedAt = AppTime.UtcNow
        };
        _db.OtpDeliveryLogs.Add(log);

        if (!result.Success)
            _audit.Queue(AuditAction.OtpDeliveryFailed, userId, email,
                $"{channel} gönderimi başarısız: {result.Error}");

        await _db.SaveChangesAsync(token);
        return log;
    }

    // Sağlayıcı geri bildirimi (webhook / durum sorgusu) geldiğinde teslim
    // durumunu günceller. Durum geriye gitmez (Delivered → Sent yazılmaz),
    // yalnızca Failed her zaman yazılabilir.
    public async Task<bool> UpdateStatusAsync(string providerMessageId, OtpDeliveryStatus status,
                                              string? error = null, CancellationToken token = default)
    {
        var log = await _db.OtpDeliveryLogs
            .FirstOrDefaultAsync(l => l.ProviderMessageId == providerMessageId, token);
        if (log == null) return false;

        if (status != OtpDeliveryStatus.Failed && status <= log.Status) return true;

        log.Status = status;
        log.Error = Trim(error, 300) ?? log.Error;
        log.StatusUpdatedAt = AppTime.UtcNow;
        await _db.SaveChangesAsync(token);
        return true;
    }

    private static string? Trim(string? s, int max) =>
        s == null ? null : s.Length > max ? s[..max] : s;
}
