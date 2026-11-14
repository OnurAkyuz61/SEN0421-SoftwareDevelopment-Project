using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Katalogda aynı isimde ikinci kategori / birim / ekip açılamamalı;
// ekip lideri yalnızca aktif Destek ya da Yönetici olabilmeli.
public class CatalogRulesTests
{
    private record Kayit(int Id, string Name);

    private static readonly List<Kayit> Mevcut = new()
    {
        new(1, "Donanım"),
        new(2, "Bilgi İşlem"),
        new(3, "Ağ / İnternet")
    };

    [Theory]
    [InlineData("Donanım")]
    [InlineData("donanım")]
    [InlineData("DONANIM")]          // Türkçe: I → ı
    [InlineData("  Donanım  ")]
    [InlineData("bilgi işlem")]
    [InlineData("BİLGİ İŞLEM")]      // Türkçe: İ → i
    [InlineData("BILGI İŞLEM")]      // Türkçe kural I≠i der, veritabanı eşit sayar: eşit kabul edilir
    [InlineData("Bilgi    İşlem")]   // aradaki fazla boşluk
    [InlineData("ağ / internet")]
    public void Ayni_isim_yeni_kayitta_reddedilir(string aday)
    {
        Assert.NotNull(CatalogRules.FindDuplicate(Mevcut, k => k.Id, k => k.Name, aday, exceptId: 0));
    }

    [Theory]
    [InlineData("Yazılım")]
    [InlineData("Donanım Destek")]
    [InlineData("Bilgi")]
    public void Farkli_isim_kabul_edilir(string aday)
    {
        Assert.Null(CatalogRules.FindDuplicate(Mevcut, k => k.Id, k => k.Name, aday, exceptId: 0));
    }

    [Fact]
    public void Kaydin_kendi_adiyla_guncellenmesi_tekrar_sayilmaz()
    {
        Assert.Null(CatalogRules.FindDuplicate(Mevcut, k => k.Id, k => k.Name, "DONANIM", exceptId: 1));
    }

    [Fact]
    public void Baska_kaydin_adina_yeniden_adlandirma_reddedilir()
    {
        var cakisan = CatalogRules.FindDuplicate(Mevcut, k => k.Id, k => k.Name, "Donanım", exceptId: 2);
        Assert.Equal(1, cakisan!.Id);
    }

    [Fact]
    public void Isim_normalize_edilir()
    {
        Assert.Equal("Bilgi İşlem", CatalogRules.NormalizeName("  Bilgi \t  İşlem "));
        Assert.Equal("", CatalogRules.NormalizeName(null));
    }

    private static AppUser Kisi(UserRole rol, bool aktif = true) =>
        new() { Id = 7, FirstName = "Test", LastName = "Kisi", Role = rol, IsActive = aktif };

    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public void Destek_ve_yonetici_ekip_lideri_olabilir(UserRole rol)
    {
        Assert.True(CatalogRules.CanLeadTeam(Kisi(rol)));
    }

    [Fact]
    public void Son_kullanici_ekip_lideri_olamaz()
    {
        Assert.False(CatalogRules.CanLeadTeam(Kisi(UserRole.User)));
    }

    [Fact]
    public void Pasif_hesap_ekip_lideri_olamaz()
    {
        Assert.False(CatalogRules.CanLeadTeam(Kisi(UserRole.Agent, aktif: false)));
    }

    [Fact]
    public void Bulunamayan_kisi_ekip_lideri_olamaz()
    {
        Assert.False(CatalogRules.CanLeadTeam(null));
    }

    // ── Alt kategori (ParentId) ──────────────────────────────────────────

    private static Dictionary<int, int?> ParentMap(params (int Id, int? ParentId)[] rows) =>
        rows.ToDictionary(r => r.Id, r => r.ParentId);

    [Fact]
    public void Kok_kategori_parent_hatasi_yok()
    {
        Assert.Null(CatalogRules.ParentError(1, null, ParentMap((1, null), (2, 1))));
    }

    [Fact]
    public void Olmayan_ust_kategori_reddedilir()
    {
        Assert.Equal("Geçerli bir üst kategori seçin.",
            CatalogRules.ParentError(1, 99, ParentMap((1, null))));
    }

    [Fact]
    public void Kendi_ust_kategorisi_olamaz()
    {
        Assert.Equal("Kategori kendi üst kategorisi olamaz.",
            CatalogRules.ParentError(1, 1, ParentMap((1, null))));
    }

    [Fact]
    public void Altindaki_kategori_ust_secilemez_dongu()
    {
        // 1 → 2 → 3; 1'in üstü olarak 3 seçilirse döngü
        var map = ParentMap((1, null), (2, 1), (3, 2));
        Assert.Contains("döngü", CatalogRules.ParentError(1, 3, map)!);
    }

    [Fact]
    public void Gecerli_ust_kategori_kabul_edilir()
    {
        Assert.Null(CatalogRules.ParentError(3, 1, ParentMap((1, null), (2, 1), (3, null))));
    }

    [Fact]
    public void Yeni_kayitta_kendisi_kontrolu_atlanir()
    {
        // id=0 yeni kayıt; henüz ağaçta yok
        Assert.Null(CatalogRules.ParentError(0, 1, ParentMap((1, null))));
    }

    [Fact]
    public void Hiyerarsi_sirasi_cocuklari_ustin_altina_koyar()
    {
        var items = new[]
        {
            new Kayit(1, "Donanım"),
            new Kayit(2, "Yazılım"),
            new Kayit(3, "Laptop"),   // Donanım altı
            new Kayit(4, "Yazıcı")    // Donanım altı
        };
        var parent = new Dictionary<int, int?> { [1] = null, [2] = null, [3] = 1, [4] = 1 };
        var sort = new Dictionary<int, int> { [1] = 10, [2] = 20, [3] = 5, [4] = 10 };

        var sirali = CatalogRules.OrderByHierarchy(items, k => k.Id, k => parent[k.Id],
            k => sort[k.Id], k => k.Name);

        Assert.Equal(new[] { 1, 3, 4, 2 }, sirali.Select(k => k.Id).ToArray());
    }
}
