using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class UserCsvImportTests
{
    [Fact]
    public void Gecerli_csv_satirlari_ayristirilir()
    {
        var csv = "Email;FirstName;LastName;Phone;Role;Department;Team;Password\n" +
                  "ali@ornek.com;Ali;Yılmaz;05321234567;Destek;Bilgi İşlem;Son Kullanıcı Desteği;\n";

        var r = UserCsvImport.Parse(csv);
        Assert.Empty(r.Errors);
        Assert.Single(r.Rows);
        Assert.Equal("ali@ornek.com", r.Rows[0].Email);
        Assert.Equal(UserRole.Agent, r.Rows[0].Role);
        Assert.Equal("05321234567", r.Rows[0].Phone);
        Assert.Equal("Bilgi İşlem", r.Rows[0].DepartmentName);
    }

    [Fact]
    public void Turkce_basliklar_kabul_edilir()
    {
        var csv = "E-posta;Ad;Soyad;Rol\nuser@x.com;Ayşe;Demir;Kullanıcı\n";
        var r = UserCsvImport.Parse(csv);
        Assert.Empty(r.Errors);
        Assert.Equal("Ayşe", r.Rows[0].FirstName);
        Assert.Equal(UserRole.User, r.Rows[0].Role);
    }

    [Fact]
    public void Gecersiz_rol_reddedilir()
    {
        var csv = "Email;FirstName;LastName;Role\na@b.com;A;B;SuperUser\n";
        var r = UserCsvImport.Parse(csv);
        Assert.Empty(r.Rows);
        Assert.Contains(r.Errors, e => e.Error.Contains("Rol"));
    }

    [Fact]
    public void Dosya_ici_tekrar_email_temizlenir()
    {
        var csv = "Email;FirstName;LastName\n" +
                  "a@b.com;A;Bir\n" +
                  "a@b.com;A;Iki\n";
        var r = UserCsvImport.Parse(csv);
        Assert.Single(r.Rows);
        Assert.Contains(r.Errors, e => e.Error.Contains("birden fazla"));
    }

    [Fact]
    public void Tirnakli_alan_ayristirilir()
    {
        var cells = UserCsvImport.SplitCsvLine("\"Yılmaz; Ali\";x;y");
        Assert.Equal(3, cells.Count);
        Assert.Equal("Yılmaz; Ali", cells[0]);
    }

    [Fact]
    public void Gecici_sifre_kurallara_uyar()
    {
        for (var i = 0; i < 20; i++)
            Assert.Null(PasswordRules.Validate(UserCsvImport.GenerateTempPassword()));
    }

    [Theory]
    [InlineData("Admin", UserRole.Admin)]
    [InlineData("yönetici", UserRole.Admin)]
    [InlineData("Agent", UserRole.Agent)]
    [InlineData("destek", UserRole.Agent)]
    [InlineData("", UserRole.User)]
    public void Rol_ayristirma(string raw, UserRole beklenen)
    {
        Assert.True(UserCsvImport.TryParseRole(raw, out var rol));
        Assert.Equal(beklenen, rol);
    }
}
