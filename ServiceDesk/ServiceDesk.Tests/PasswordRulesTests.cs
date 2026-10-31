using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class PasswordRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Bos_deger_opsiyonel_alandan_gecer(string? sifre)
    {
        Assert.Null(PasswordRules.Validate(sifre));
    }

    [Theory]
    [InlineData("Abc1!")]           // kısa
    [InlineData("abcdefgh")]        // yalnızca küçük
    [InlineData("ABCDEFGH")]        // yalnızca büyük
    [InlineData("abcd1234")]        // özel karakter yok
    [InlineData("ABCD1234")]        // küçük yok, özel yok
    [InlineData("Abcdefgh")]        // rakam ve özel yok
    [InlineData("Admin123")]        // özel karakter yok (eski seed tarzı)
    public void Zayif_sifre_reddedilir(string sifre)
    {
        Assert.NotNull(PasswordRules.Validate(sifre));
    }

    [Theory]
    [InlineData("Admin123!")]
    [InlineData("GucLu#Sifre1")]
    [InlineData("aB3$xxxx")]
    public void Guclu_sifre_kabul_edilir(string sifre)
    {
        Assert.Null(PasswordRules.Validate(sifre));
    }

    [Fact]
    public void Cok_uzun_sifre_reddedilir()
    {
        var uzun = new string('a', PasswordRules.MaxLength) + "A1!";
        Assert.NotNull(PasswordRules.Validate(uzun));
    }
}
