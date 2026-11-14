using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Süresi dolmuş veya kullanılmış şifre sıfırlama token'larını ve kullanıcı
// davetlerini siler. Ham token zaten saklanmaz (yalnızca hash); yine de ölü
// satırlar birikmesin ve çalınmış hash'lerin yeniden deneme yüzeyi daralsın.
//
// Desen: AttachmentRetentionService / SlaMonitorService —
// BackgroundService + PeriodicTimer + IServiceScopeFactory.
public class PasswordResetCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly AuthSettings _cfg;
    private readonly ILogger<PasswordResetCleanupService> _log;

    public PasswordResetCleanupService(IServiceScopeFactory scopes, IOptions<AuthSettings> cfg,
                                       ILogger<PasswordResetCleanupService> log)
    {
        _scopes = scopes;
        _cfg = cfg.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cfg.PasswordResetCleanupEnabled)
        {
            _log.LogInformation("Şifre sıfırlama token temizliği kapalı (Auth:PasswordResetCleanupEnabled)");
            return;
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(Math.Max(1, _cfg.PasswordResetCleanupMinutes)));

        do
        {
            try
            {
                var silinen = await RunOnceAsync(stoppingToken);
                if (silinen > 0)
                    _log.LogInformation("Token / davet temizliği: {Count} kayıt silindi", silinen);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Şifre sıfırlama token temizliği başarısız oldu");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    // Kullanılmış veya süresi dolmuş → temizlenmeli (saf; birim test)
    public static bool IsStale(PasswordResetToken token, DateTime nowUtc) =>
        token.UsedAt != null || token.ExpiresAt < nowUtc;

    public static bool IsStaleInvite(UserInvite invite, DateTime nowUtc) =>
        invite.UsedAt != null || invite.ExpiresAt < nowUtc;

    // Bir tur: en fazla BatchSize satır siler, silinen sayıyı döner.
    public async Task<int> RunOnceAsync(CancellationToken token = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = Math.Max(1, _cfg.PasswordResetCleanupBatchSize);
        var now = AppTime.UtcNow;
        return await PurgeAsync(db, now, batch, token)
             + await PurgeInvitesAsync(db, now, batch, token);
    }

    // EF sorgusu — testlerde InMemory / gerçek context ile çağrılabilir
    public static async Task<int> PurgeAsync(AppDbContext db, DateTime nowUtc, int batchSize,
                                            CancellationToken token = default)
    {
        var stale = await db.PasswordResetTokens
            .Where(t => t.UsedAt != null || t.ExpiresAt < nowUtc)
            .OrderBy(t => t.ExpiresAt)
            .Take(Math.Max(1, batchSize))
            .ToListAsync(token);

        if (stale.Count == 0) return 0;

        db.PasswordResetTokens.RemoveRange(stale);
        await db.SaveChangesAsync(token);
        return stale.Count;
    }

    public static async Task<int> PurgeInvitesAsync(AppDbContext db, DateTime nowUtc, int batchSize,
                                                   CancellationToken token = default)
    {
        var stale = await db.UserInvites
            .Where(i => i.UsedAt != null || i.ExpiresAt < nowUtc)
            .OrderBy(i => i.ExpiresAt)
            .Take(Math.Max(1, batchSize))
            .ToListAsync(token);

        if (stale.Count == 0) return 0;

        db.UserInvites.RemoveRange(stale);
        await db.SaveChangesAsync(token);
        return stale.Count;
    }
}
