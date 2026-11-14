using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Periyodik şifre yenileme hesabı — filtre, CurrentUser ve view ortak kullanır.
public static class PasswordPolicy
{
    public static bool IsExpired(AppUser user, AuthSettings cfg)
    {
        if (cfg.PasswordMaxAgeDays <= 0) return false;
        var since = user.PasswordChangedAt ?? user.CreatedAt;
        return AppTime.UtcNow - since > TimeSpan.FromDays(cfg.PasswordMaxAgeDays);
    }

    public static string ExpiredMessage(AuthSettings cfg) =>
        $"Şifrenizin süresi doldu ({cfg.PasswordMaxAgeDays} gün). Devam etmek için yeni şifre belirleyin.";
}
