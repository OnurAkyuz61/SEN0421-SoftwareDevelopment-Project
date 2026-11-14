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

    // Üst kategori ataması: kendisi olamaz, döngü olamaz, hedef gerçekten var olmalı.
    // null = kök kategori (üst yok).
    public static string? ParentError(int categoryId, int? parentId,
                                      IReadOnlyDictionary<int, int?> parentById)
    {
        if (parentId is null) return null;

        if (!parentById.ContainsKey(parentId.Value))
            return "Geçerli bir üst kategori seçin.";

        if (categoryId > 0 && parentId.Value == categoryId)
            return "Kategori kendi üst kategorisi olamaz.";

        // parent → … → categoryId zinciri varsa döngü
        if (categoryId > 0)
        {
            var seen = new HashSet<int>();
            var walk = parentId;
            while (walk is int pid)
            {
                if (pid == categoryId)
                    return "Seçilen üst kategori, bu kategorinin altındadır; döngü oluşur.";
                if (!seen.Add(pid)) break; // bozuk veri koruması
                walk = parentById.TryGetValue(pid, out var next) ? next : null;
            }
        }

        return null;
    }

    // Katalog / açılır liste için: kökler önce, çocuklar üstlerinin hemen altında.
    public static List<T> OrderByHierarchy<T>(IEnumerable<T> items,
                                              Func<T, int> id,
                                              Func<T, int?> parentId,
                                              Func<T, int> sortOrder,
                                              Func<T, string> name)
    {
        var list = items.ToList();
        var byParent = list.GroupBy(x => parentId(x) ?? 0)
            .ToDictionary(g => g.Key, g => g.OrderBy(sortOrder).ThenBy(name).ToList());

        var result = new List<T>(list.Count);
        void Visit(int parentKey)
        {
            if (!byParent.TryGetValue(parentKey, out var kids)) return;
            foreach (var kid in kids)
            {
                result.Add(kid);
                Visit(id(kid));
            }
        }
        Visit(0);
        // Yetim (üst silinmiş) kayıtlar sonda
        foreach (var orphan in list.Where(x => !result.Contains(x)).OrderBy(sortOrder).ThenBy(name))
            result.Add(orphan);
        return result;
    }
}
