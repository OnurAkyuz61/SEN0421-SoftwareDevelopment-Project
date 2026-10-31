namespace ServiceDesk.Services;

// Yeni şifre kuralları. Önceden yalnızca uzunluk (en az 8 karakter) bakılıyordu;
// "admin123" gibi yalnızca küçük harf + rakam şifreler kabul ediliyordu.
//
// Yeni şifre (profil değiştirme, şifre sıfırlama, kullanıcı oluşturma/düzenleme):
//   • en az 8, en fazla 128 karakter
//   • en az bir büyük harf, bir küçük harf, bir rakam ve bir özel karakter
//
// Boş değer geçerli sayılır: yönetim ekranında şifre alanı opsiyoneldir
// (boş bırakılırsa mevcut şifre değişmez). Zorunluluk [Required] veya
// controller kontrolüyle ayrıca sağlanır.
//
// Mevcut hesapların zayıf seed şifreleri (admin123 vb.) girişte hâlâ çalışır;
// bu kurallar yalnızca yeni belirlenen şifrelere uygulanır.
//
// Saf sınıf: birim testi yazılabiliyor (bkz. ServiceDesk.Tests).
public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public const string Hint =
        "En az 8 karakter; en az bir büyük harf, bir küçük harf, bir rakam ve bir özel karakter (!@#$%...).";

    // Boş / null → geçerli (opsiyonel alan). Doluysa kurallar; hata mesajı veya null.
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password)) return null;

        if (password.Length < MinLength)
            return $"Şifre en az {MinLength} karakter olmalı.";
        if (password.Length > MaxLength)
            return $"Şifre en fazla {MaxLength} karakter olabilir.";

        var buyuk = false;
        var kucuk = false;
        var rakam = false;
        var ozel = false;
        foreach (var c in password)
        {
            if (char.IsUpper(c)) buyuk = true;
            else if (char.IsLower(c)) kucuk = true;
            else if (char.IsDigit(c)) rakam = true;
            else ozel = true;
        }

        return buyuk && kucuk && rakam && ozel
            ? null
            : "Şifre en az bir büyük harf, bir küçük harf, bir rakam ve bir özel karakter (!@#$%...) içermeli.";
    }
}
