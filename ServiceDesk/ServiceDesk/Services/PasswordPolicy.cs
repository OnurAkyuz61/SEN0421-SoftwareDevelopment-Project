using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Şifre yenileme zorunluluğu — filtre, CurrentUser ve view ortak kullanır.
//   1) MustChangePassword: ilk giriş / yönetici geçici şifresi
//   2) PasswordMaxAgeDays: periyodik yenileme
public static class PasswordPolicy
{
    public static bool IsExpired(AppUser user, AuthSettings cfg)
    {
        if (cfg.PasswordMaxAgeDays <= 0) return false;
        var since = user.PasswordChangedAt ?? user.CreatedAt;
        return AppTime.UtcNow - since > TimeSpan.FromDays(cfg.PasswordMaxAgeDays);
    }

    public static bool RequiresChange(AppUser user, AuthSettings cfg) =>
        user.MustChangePassword || IsExpired(user, cfg);

    public static string RequiredMessage(AppUser user, AuthSettings cfg)
    {
        if (user.MustChangePassword)
            return "İlk girişte şifrenizi değiştirmeniz gerekiyor. Devam etmek için yeni şifre belirleyin.";
        return ExpiredMessage(cfg);
    }

    public static string ExpiredMessage(AuthSettings cfg) =>
        $"Şifrenizin süresi doldu ({cfg.PasswordMaxAgeDays} gün). Devam etmek için yeni şifre belirleyin.";
}
