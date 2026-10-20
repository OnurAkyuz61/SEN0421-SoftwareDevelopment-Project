namespace ServiceDesk.Services;

// Oturum ve OTP ile ilgili tüm süreler / sınırlar tek yerde.
// appsettings.json → "Auth" bölümünden okunur; değer verilmezse aşağıdaki
// varsayılanlar kullanılır.
public class AuthSettings
{
    public const string Section = "Auth";
    public const string CookieName = ".ServiceDesk.Session";

    // Hiç işlem yapılmazsa oturumun kapanacağı süre (kayan süre)
    public int IdleTimeoutMinutes { get; set; } = 30;

    // İşlem yapılsa bile oturumun en fazla açık kalabileceği süre
    public int AbsoluteTimeoutHours { get; set; } = 8;

    // Oturum bitmeden kaç saniye önce "oturumunuz kapanacak" uyarısı çıksın
    public int WarningBeforeSeconds { get; set; } = 120;

    // Şifre doğrulandıktan sonra OTP adımının tamamlanması için verilen süre
    public int PendingLoginMinutes { get; set; } = 10;

    // Gönderilen kodun geçerlilik süresi
    public int OtpLifetimeSeconds { get; set; } = 300;

    // "Tekrar gönder" için iki gönderim arasındaki bekleme
    public int OtpResendCooldownSeconds { get; set; } = 30;

    // Bir giriş denemesinde en fazla kaç kez yeni kod istenebilir
    public int OtpMaxSends { get; set; } = 4;

    // Aynı kod için en fazla hatalı deneme; aşılırsa kod geçersiz olur
    public int OtpMaxAttempts { get; set; } = 5;

    // Şifre sıfırlama bağlantısının geçerlilik süresi (saat)
    public int PasswordResetHours { get; set; } = 1;
}

// Session anahtarları — yazım hatası olmasın diye sabit
public static class SessionKeys
{
    // Giriş tamamlanınca
    public const string UserId = "Auth.UserId";
    public const string IssuedAt = "Auth.IssuedAt";

    // Şifre doğrulandı, OTP bekleniyor
    public const string PendingUserId = "Otp.UserId";
    public const string PendingPhone = "Otp.Phone";
    public const string PendingStartedAt = "Otp.StartedAt";
    public const string ReturnUrl = "Otp.ReturnUrl";

    // Gönderilen kod
    public const string Code = "Otp.Code";
    public const string Channel = "Otp.Channel";
    public const string ExpiresAt = "Otp.Expires";
    public const string SentAt = "Otp.SentAt";
    public const string SendCount = "Otp.SendCount";
    public const string Attempts = "Otp.Attempts";
}
