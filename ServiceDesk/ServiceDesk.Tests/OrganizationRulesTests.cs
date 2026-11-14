using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Kullanıcı birim ↔ ekip tutarlılığı: birim değişince ekip doğrulanır.
public class OrganizationRulesTests
{
    [Fact]
    public void Ekip_yoksa_birim_oldugu_gibi_kalir()
    {
        var r = OrganizationRules.Resolve(departmentId: 1, teamId: null, teamFound: false, teamDepartmentId: null);
        Assert.Equal(1, r.DepartmentId);
        Assert.Null(r.TeamId);
        Assert.Null(r.Error);
    }

    [Fact]
    public void Olmayan_ekip_reddedilir()
    {
        var r = OrganizationRules.Resolve(1, teamId: 99, teamFound: false, teamDepartmentId: null);
        Assert.NotNull(r.Error);
        Assert.Contains("Geçerli bir ekip", r.Error);
    }

    [Fact]
    public void Birim_bos_ekip_doluysa_birim_ekipten_dolur()
    {
        var r = OrganizationRules.Resolve(null, teamId: 2, teamFound: true, teamDepartmentId: 5);
        Assert.Equal(5, r.DepartmentId);
        Assert.Equal(2, r.TeamId);
        Assert.Null(r.Error);
    }

    [Fact]
    public void Birim_degisince_uyumsuz_ekip_reddedilir()
    {
        // Eski ekip birim 1'de, yeni birim 3
        var r = OrganizationRules.Resolve(3, teamId: 2, teamFound: true, teamDepartmentId: 1);
        Assert.NotNull(r.Error);
        Assert.Contains("birime bağlı değil", r.Error);
    }

    [Fact]
    public void Ayni_birime_ait_ekip_kabul_edilir()
    {
        var r = OrganizationRules.Resolve(1, teamId: 2, teamFound: true, teamDepartmentId: 1);
        Assert.Null(r.Error);
        Assert.Equal(1, r.DepartmentId);
        Assert.Equal(2, r.TeamId);
    }

    [Theory]
    [InlineData(null, 1, true)]
    [InlineData(1, null, true)]
    [InlineData(1, 1, true)]
    [InlineData(1, 2, false)]
    public void TeamMatchesDepartment_uyumu_dogru_bulur(int? birim, int? ekipBirimi, bool beklenen)
    {
        Assert.Equal(beklenen, OrganizationRules.TeamMatchesDepartment(birim, ekipBirimi));
    }
}
