using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Şifre geçmişi: eski özetleri saklar, yeni şifrenin son N özetten biriyle
// aynı olmasını engeller. Düz metin asla yazılmaz.
public class PasswordHistoryService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;
    private readonly AuthSettings _cfg;

    public PasswordHistoryService(AppDbContext db, IPasswordHasher<AppUser> hasher,
                                  IOptions<AuthSettings> cfg)
    {
        _db = db;
        _hasher = hasher;
        _cfg = cfg.Value;
    }

    public int HistoryCount => Math.Max(1, _cfg.PasswordHistoryCount);

    // null = uygun; aksi halde kullanıcıya gösterilecek hata
    public async Task<string?> ReuseErrorAsync(AppUser user, string newPassword)
    {
        if (string.IsNullOrEmpty(newPassword)) return null;

        if (!string.IsNullOrEmpty(user.PasswordHash) &&
            _hasher.VerifyHashedPassword(user, user.PasswordHash, newPassword)
                != PasswordVerificationResult.Failed)
            return "Yeni şifre mevcut şifrenizle aynı olamaz.";

        if (user.Id <= 0 || HistoryCount <= 1) return null;

        // Mevcut + geçmiş = HistoryCount; tabloda en fazla HistoryCount-1 özet
        var past = await _db.PasswordHistories.AsNoTracking()
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.CreatedAt)
            .Take(HistoryCount - 1)
            .Select(h => h.PasswordHash)
            .ToListAsync();

        foreach (var hash in past)
        {
            if (_hasher.VerifyHashedPassword(user, hash, newPassword)
                != PasswordVerificationResult.Failed)
                return $"Son {HistoryCount} şifrenizden biri tekrar kullanılamaz.";
        }

        return null;
    }

    // Mevcut özeti geçmişe alır, yeni özeti yazar, fazla kayıtları budar.
    // SaveChanges çağıranın sorumluluğundadır (aynı transaction'da kalsın).
    //
    // requireChangeOnNextLogin: yönetici geçici şifre verdiğinde true —
    // kullanıcı bir sonraki girişte kendi şifresini seçmek zorunda kalır.
    // Kullanıcı kendi değiştirdiyse / sıfırladıysa false (bayrak kalkar).
    public async Task SetPasswordAsync(AppUser user, string newPassword,
                                       bool requireChangeOnNextLogin = false)
    {
        if (user.Id > 0 && !string.IsNullOrEmpty(user.PasswordHash) && HistoryCount > 1)
        {
            // Yeni satır için yer aç: en fazla HistoryCount-2 eski kalsın
            await EnsureMaxAsync(user.Id, HistoryCount - 2);
            _db.PasswordHistories.Add(new PasswordHistory
            {
                UserId = user.Id,
                PasswordHash = user.PasswordHash,
                CreatedAt = AppTime.UtcNow
            });
        }

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        user.PasswordChangedAt = AppTime.UtcNow;
        user.MustChangePassword = requireChangeOnNextLogin;
    }

    private async Task EnsureMaxAsync(int userId, int max)
    {
        if (max < 0) max = 0;
        var rows = await _db.PasswordHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();
        if (rows.Count <= max) return;
        _db.PasswordHistories.RemoveRange(rows.Skip(max));
    }
}
