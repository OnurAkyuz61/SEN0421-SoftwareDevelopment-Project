using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Arama ve OTP uçlarına istek sınırı (ASP.NET Core yerleşik rate limiter).
// Değerler appsettings.json → "RateLimiting" bölümünden okunur.
//
//   • otp    : OTP kanal seçimi / tekrar gönder / kod doğrulama.
//              İstemci IP'si + bekleyen giriş (session) başına sabit pencere.
//              Kod tahmini ve SMS bombardımanına karşı; hesap bazlı kilit
//              (LoginAuditService) bunun üzerine ikinci katmandır.
//   • search : talep, arşiv, bilgi bankası, kullanıcı ve onay aramaları.
//              Oturumdaki kullanıcı (yoksa IP) başına kayan pencere.
public static class RateLimitPolicies
{
    public const string Otp = "otp";
    public const string Search = "search";
}

public class RateLimitSettings
{
    public const string Section = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public int OtpPermitLimit { get; set; } = 10;
    public int OtpWindowSeconds { get; set; } = 300;

    public int SearchPermitLimit { get; set; } = 60;
    public int SearchWindowSeconds { get; set; } = 60;
    public int SearchSegments { get; set; } = 6;
}

public static class RateLimitingSetup
{
    public static IServiceCollection AddServiceDeskRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        var cfg = config.GetSection(RateLimitSettings.Section).Get<RateLimitSettings>() ?? new RateLimitSettings();

        services.AddRateLimiter(opt =>
        {
            opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            opt.AddPolicy(RateLimitPolicies.Otp, ctx =>
            {
                if (!cfg.Enabled) return RateLimitPartition.GetNoLimiter("off");

                // IP + bekleyen kullanıcı: aynı NAT arkasındaki farklı kullanıcılar
                // birbirini kilitlemez, aynı kişinin farklı IP'leri de ayrı sayılır
                var pending = TryGetSessionInt(ctx, SessionKeys.PendingUserId);
                var key = $"otp:{ctx.Connection.RemoteIpAddress}:{pending}";

                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = cfg.OtpPermitLimit,
                    Window = TimeSpan.FromSeconds(cfg.OtpWindowSeconds),
                    QueueLimit = 0
                });
            });

            opt.AddPolicy(RateLimitPolicies.Search, ctx =>
            {
                if (!cfg.Enabled) return RateLimitPartition.GetNoLimiter("off");

                var user = TryGetSessionInt(ctx, SessionKeys.UserId);
                var key = user != null ? $"search:u{user}" : $"search:ip{ctx.Connection.RemoteIpAddress}";

                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = cfg.SearchPermitLimit,
                    Window = TimeSpan.FromSeconds(cfg.SearchWindowSeconds),
                    SegmentsPerWindow = Math.Max(1, cfg.SearchSegments),
                    QueueLimit = 0
                });
            });

            // Reddedilen istek: Retry-After başlığı + denetim kaydı + Türkçe hata sayfası
            opt.OnRejected = async (context, token) =>
            {
                var http = context.HttpContext;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    http.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                try
                {
                    var audit = http.RequestServices.GetRequiredService<AuditService>();
                    var userId = TryGetSessionInt(http, SessionKeys.UserId) ?? TryGetSessionInt(http, SessionKeys.PendingUserId);
                    await audit.WriteAsync(AuditAction.RateLimited, userId, detail: $"{http.Request.Method} {http.Request.Path}");
                }
                catch
                {
                    // Denetim kaydı yazılamasa da 429 yanıtı dönmeli
                }

                var wantsJson = http.Request.Headers.Accept.ToString().Contains("application/json");
                if (wantsJson)
                {
                    await http.Response.WriteAsJsonAsync(new { error = "Çok fazla istek. Lütfen biraz bekleyin." }, token);
                }
                else
                {
                    http.Response.ContentType = "text/html; charset=utf-8";
                    await http.Response.WriteAsync(
                        "<!doctype html><meta charset=utf-8><title>Çok fazla istek</title>" +
                        "<div style=\"font-family:sans-serif;max-width:480px;margin:80px auto;text-align:center\">" +
                        "<h2>Çok fazla istek gönderildi</h2><p>Kısa sürede çok sayıda istek alındı. " +
                        "Lütfen biraz bekleyip yeniden deneyin.</p><p><a href=\"javascript:history.back()\">Geri dön</a></p></div>",
                        token);
                }
            };
        });

        return services;
    }

    private static int? TryGetSessionInt(HttpContext ctx, string key)
    {
        try { return ctx.Session.IsAvailable ? ctx.Session.GetInt32(key) : null; }
        catch (InvalidOperationException) { return null; }
    }
}
