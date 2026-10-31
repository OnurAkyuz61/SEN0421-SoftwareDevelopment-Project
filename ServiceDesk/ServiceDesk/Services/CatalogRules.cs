using System.Globalization;
using System.Text.RegularExpressions;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Katalog (kategori / birim / ekip) kayıtlarının kuralları.
//
// İsim benzersizliği: "Donanım", " donanım " ve "DONANIM" aynı kayıt sayılır.
// Büyük-küçük harf Türkçe kurallarla karşılaştırılır (I/ı, İ/i ayrı harfler),
// baştaki / sondaki ve aradaki fazla boşluklar yok sayılır. Pasif kayıtlar da
// sayılır: pasif "Donanım" dururken ikinci bir "Donanım" açılırsa raporlarda
// iki ayrı satır olarak görünür; doğrusu eskisini yeniden aktifleştirmektir.
//
// Saf sınıf: birim testi yazılabiliyor (bkz. ServiceDesk.Tests).
public static class CatalogRules
{
    public const int MaxNameLength = 100;

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    // Kaydedilecek biçim: kırpılmış, aradaki boşluklar teke indirilmiş
    public static string NormalizeName(string? name) =>
        Spaces.Replace(name ?? "", " ").Trim();

    // Türkçe kural "BILGI" ile "Bilgi"yi farklı sayar (I ≠ i), veritabanının
    // varsayılan karşılaştırması ise aynı sayar. İkisinden biri eşit diyorsa
    // eşittir: böylece uygulama kontrolü geçip veritabanı indeksine takılan
    // (kullanıcıya anlamsız hata gösteren) bir durum kalmaz.
    public static bool SameName(string a, string b)
    {
        var x = NormalizeName(a);
        var y = NormalizeName(b);
        return string.Compare(x, y, Tr, CompareOptions.IgnoreCase) == 0
            || string.Equals(x, y, StringComparison.InvariantCultureIgnoreCase);
    }

    // Aynı isimde başka bir kayıt var mı (düzenlenen kaydın kendisi hariç)?
    // Varsa çakışan kaydı döner; mesajda pasif olup olmadığı söylenebilsin diye.
    public static T? FindDuplicate<T>(IEnumerable<T> existing, Func<T, int> id, Func<T, string> name,
                                      string candidate, int exceptId) where T : class =>
        existing.FirstOrDefault(e => id(e) != exceptId && SameName(name(e), candidate));

    // Ekip lideri: eskalasyonun ikinci seviyesi bu kişiye yükselir, yani talep
    // üzerinde işlem yapabilmeli. Son kullanıcı (User) talepleri yalnızca
    // portaldan görür; lider olursa eskalasyon boşa düşer.
    public static bool CanLeadTeam(AppUser? user) =>
        user is { IsActive: true } && user.Role is UserRole.Agent or UserRole.Admin;
}
