using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Güvenlik ve süreç olaylarını AuditLogs tablosuna yazar. IP ve tarayıcı
// bilgisi istekten okunur; arka plan servislerinde boş kalır.
public class AuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    // Kaydı ekler, SaveChanges çağırmaz (çağıranın işlemiyle birlikte yazılır)
    public void Queue(AuditAction action, int? userId = null, string? email = null,
                      string? detail = null, int? ticketId = null)
    {
        var ctx = _http.HttpContext;
        var agent = ctx?.Request.Headers.UserAgent.ToString();

        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            UserId = userId,
            Email = string.IsNullOrWhiteSpace(email) ? null : Trim(email.Trim().ToLowerInvariant(), 150),
            TicketId = ticketId,
            Detail = detail == null ? null : Trim(detail, 1000),
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = string.IsNullOrEmpty(agent) ? null : Trim(agent, 300),
            CreatedAt = AppTime.UtcNow
        });
    }

    // Tek başına yazılan olaylar için (ör. başarısız OTP: başka kayıt yok)
    public async Task WriteAsync(AuditAction action, int? userId = null, string? email = null,
                                 string? detail = null, int? ticketId = null)
    {
        Queue(action, userId, email, detail, ticketId);
        await _db.SaveChangesAsync();
    }

    private static string Trim(string s, int max) => s.Length > max ? s[..max] : s;
}
