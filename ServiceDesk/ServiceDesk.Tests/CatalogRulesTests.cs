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
}
