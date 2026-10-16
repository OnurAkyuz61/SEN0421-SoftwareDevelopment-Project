using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Giriş denemelerini kaydeder ve art arda başarısız denemeleri sınırlar.
// Önceden hiçbir deneme kaydedilmiyordu: bir hesaba binlerce şifre denenebilir
// veya OTP kodu sınırsız tahmin edilebilirdi ve hiçbir iz kalmazdı.
public class LoginAuditService
{
    // Bu kadar başarısız denemeden sonra, bu süre boyunca giriş kapanır
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public LoginAuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task RecordAsync(string email, LoginResult result, int? userId = null)
    {
        _db.LoginAttempts.Add(new LoginAttempt
        {
            Email = (email ?? "").Trim().ToLowerInvariant(),
            UserId = userId,
            Result = result,
            IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            AttemptedAt = AppTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    // Son pencerede kaç başarısız deneme var? Araya başarılı bir giriş
    // girdiyse sayaç oradan itibaren sayılır.
    public async Task<int> RecentFailuresAsync(string email)
    {
        var normalized = (email ?? "").Trim().ToLowerInvariant();
        var since = AppTime.UtcNow - Window;

        var attempts = await _db.LoginAttempts
            .AsNoTracking()
            .Where(l => l.Email == normalized && l.AttemptedAt >= since)
            .OrderByDescending(l => l.AttemptedAt)
            .Select(l => l.Result)
            .ToListAsync();

        var failures = 0;
        foreach (var r in attempts)
        {
            if (r == LoginResult.Success) break;
            failures++;
        }
        return failures;
    }

    public async Task<bool> IsLockedOutAsync(string email) =>
        await RecentFailuresAsync(email) >= MaxFailedAttempts;

    public int RemainingAttempts(int failures) => Math.Max(0, MaxFailedAttempts - failures);
}
