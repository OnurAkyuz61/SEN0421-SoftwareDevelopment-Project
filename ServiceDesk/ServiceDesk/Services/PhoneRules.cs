namespace ServiceDesk.Services;

// Telefon numarası kuralları. İki adımlı giriş (OTP) bu numaraya gider; bu
// yüzden numara yalnızca bir iletişim bilgisi değil, giriş anahtarıdır.
//
//  • Biçim: boşluk, tire, nokta ve parantez atılır ("0532 123-45-67" ve
//    "05321234567" aynı numaradır). Böylece aynı numara farklı yazılarak iki
//    hesaba kaydedilemez ve OTP ekranındaki maskeleme her zaman çalışır.
//  • Silme: aktif bir hesaptaki numara boşaltılamaz, yalnızca değiştirilebilir.
//    Önceden kullanıcı profilinden numarasını silince bir sonraki girişte
//    "telefon tanımlı değil" hatası alıyor ve hesabına bir daha giremiyordu.
//
// Saf sınıf: birim testi yazılabiliyor (bkz. ServiceDesk.Tests).
public static class PhoneRules
{
    public const int MinDigits = 10;
    public const int MaxDigits = 15; // E.164 üst sınırı

    // Boş girişte null; aksi halde yalnızca rakamlar (başta isteğe bağlı "+")
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        var plus = t.StartsWith('+');
        var digits = new string(t.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : (plus ? "+" : "") + digits;
    }

    // Normalize edilmiş numara için hata mesajı; geçerliyse null
    public static string? Validate(string? normalized)
    {
        if (normalized == null) return null;
        var digits = normalized.TrimStart('+').Length;
        return digits < MinDigits || digits > MaxDigits
            ? $"Telefon numarası {MinDigits}–{MaxDigits} haneden oluşmalı (ör. 0532 123 45 67)."
            : null;
    }

    // Kayıtlı numara boşaltılmak isteniyorsa engellenir: hesap aktifken OTP'siz
    // kalırsa giriş yapılamaz
    public static bool WouldLockOut(string? currentPhone, string? newPhone, bool isActive) =>
        isActive && !string.IsNullOrWhiteSpace(currentPhone) && newPhone == null;

    public const string RemoveBlockedMessage =
        "Telefon numarası silinemez: giriş doğrulama kodu (OTP) bu numaraya gönderilir, " +
        "numara silinirse hesaba bir daha giriş yapılamaz. Numarayı değiştirebilirsiniz.";
}
