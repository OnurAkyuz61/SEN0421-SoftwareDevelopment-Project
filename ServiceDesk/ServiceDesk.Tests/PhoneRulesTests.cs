using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Profilden / yönetim ekranından telefon silinince OTP girişi bozulmamalı.
public class PhoneRulesTests
{
    [Theory]
    [InlineData("0532 123 45 67", "05321234567")]
    [InlineData("0532-123-45-67", "05321234567")]
    [InlineData("(0532) 123 45 67", "05321234567")]
    [InlineData("+90 532 123 45 67", "+905321234567")]
    [InlineData("05321234567", "05321234567")]
    public void Ayni_numaranin_farkli_yazimlari_tek_bicime_iner(string giris, string beklenen)
    {
        Assert.Equal(beklenen, PhoneRules.Normalize(giris));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" - ")]
    public void Bos_giris_null_olur(string? giris)
    {
        Assert.Null(PhoneRules.Normalize(giris));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567890123456")]
    public void Gecersiz_uzunluk_reddedilir(string giris)
    {
        Assert.NotNull(PhoneRules.Validate(PhoneRules.Normalize(giris)));
    }

    [Fact]
    public void Aktif_hesabin_numarasi_silinemez()
    {
        Assert.True(PhoneRules.WouldLockOut("05321234567", null, isActive: true));
    }

    [Fact]
    public void Numara_degistirilebilir()
    {
        Assert.False(PhoneRules.WouldLockOut("05321234567", "05329999999", isActive: true));
    }

    [Fact]
    public void Zaten_telefonsuz_hesap_kilitlenmis_sayilmaz()
    {
        Assert.False(PhoneRules.WouldLockOut(null, null, isActive: true));
    }

    [Fact]
    public void Pasif_hesabin_numarasi_silinebilir()
    {
        Assert.False(PhoneRules.WouldLockOut("05321234567", null, isActive: false));
    }
}
