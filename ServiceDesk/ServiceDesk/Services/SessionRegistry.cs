using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Aynı hesapta açık oturumları izler ve üst sınırı uygular.
// Yeni girişte jeton üretilir; limit aşılınca en eski kayıt(lar) silinir —
// o tarayıcılar bir sonraki istekte CurrentUser tarafından düşürülür.
public class SessionRegistry
{
    private readonly AppDbContext _db;
    private readonly AuthSettings _cfg;
    private readonly IHttpContextAccessor _http;

    public SessionRegistry(AppDbContext db, IOptions<AuthSettings> cfg, IHttpContextAccessor http)
    {
        _db = db;
        _cfg = cfg.Value;
        _http = http;
    }

    // Başarılı giriş sonrası çağrılır. Üretilen jeton Session'a yazılmalıdır.
    public async Task<string> OpenAsync(int userId)
    {
        var max = Math.Max(1, _cfg.MaxConcurrentSessions);
        var now = AppTime.UtcNow;
        var absolute = TimeSpan.FromHours(Math.Max(1, _cfg.AbsoluteTimeoutHours));

        // Süresi dolmuş kayıtları temizle (mutlak oturum süresi)
        var staleBefore = now - absolute;
        var stale = await _db.UserActiveSessions
            .Where(s => s.UserId == userId && s.CreatedAt < staleBefore)
            .ToListAsync();
        if (stale.Count > 0) _db.UserActiveSessions.RemoveRange(stale);

        var token = Guid.NewGuid().ToString("D");
        _db.UserActiveSessions.Add(new UserActiveSession
        {
            UserId = userId,
            Token = token,
            CreatedAt = now,
            LastSeenAt = now,
            IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString()
        });

        await _db.SaveChangesAsync();

        // En yeni max kadar oturum kalsın; fazlası (en eski) düşer
        var keepIds = await _db.UserActiveSessions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(max)
            .Select(s => s.Id)
            .ToListAsync();

        var excess = await _db.UserActiveSessions
            .Where(s => s.UserId == userId && !keepIds.Contains(s.Id))
            .ToListAsync();
        if (excess.Count > 0)
        {
            _db.UserActiveSessions.RemoveRange(excess);
            await _db.SaveChangesAsync();
        }

        return token;
    }

    public bool IsOpen(int userId, string token) =>
        !string.IsNullOrEmpty(token) &&
        _db.UserActiveSessions.Any(s => s.UserId == userId && s.Token == token);

    public async Task CloseAsync(int userId, string? token)
    {
        if (string.IsNullOrEmpty(token)) return;
        var row = await _db.UserActiveSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Token == token);
        if (row == null) return;
        _db.UserActiveSessions.Remove(row);
        await _db.SaveChangesAsync();
    }

    // Pasifleştirme / zorla çıkış: hesaptaki tüm açık oturumlar
    public async Task CloseAllAsync(int userId)
    {
        var rows = await _db.UserActiveSessions.Where(s => s.UserId == userId).ToListAsync();
        if (rows.Count == 0) return;
        _db.UserActiveSessions.RemoveRange(rows);
        await _db.SaveChangesAsync();
    }
}
