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

    [Fact]
    public void Gecmis_ipuucu_son_n_sifreyi_belirtir()
    {
        Assert.Contains("5", PasswordRules.HistoryHint(5));
        Assert.Contains("tekrar kullanılamaz", PasswordRules.HistoryHint(5));
    }
}

public class PasswordPolicyTests
{
    private static AuthSettings Cfg(int days) => new() { PasswordMaxAgeDays = days };

    [Fact]
    public void Max_yas_sifirsa_suresi_dolmaz()
    {
        var u = new ServiceDesk.Models.AppUser
        {
            CreatedAt = AppTime.UtcNow.AddYears(-2),
            PasswordChangedAt = AppTime.UtcNow.AddYears(-1)
        };
        Assert.False(PasswordPolicy.IsExpired(u, Cfg(0)));
    }

    [Fact]
    public void Degisim_tarihi_eskiyse_suresi_dolmus_sayilir()
    {
        var u = new ServiceDesk.Models.AppUser
        {
            CreatedAt = AppTime.UtcNow.AddDays(-200),
            PasswordChangedAt = AppTime.UtcNow.AddDays(-100)
        };
        Assert.True(PasswordPolicy.IsExpired(u, Cfg(90)));
    }

    [Fact]
    public void Degisim_tarihi_yoksa_CreatedAt_kullanilir()
    {
        var u = new ServiceDesk.Models.AppUser
        {
            CreatedAt = AppTime.UtcNow.AddDays(-100),
            PasswordChangedAt = null
        };
        Assert.True(PasswordPolicy.IsExpired(u, Cfg(90)));
        Assert.False(PasswordPolicy.IsExpired(u, Cfg(120)));
    }
}
