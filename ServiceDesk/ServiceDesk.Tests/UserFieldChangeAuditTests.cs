using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class UserFieldChangeAuditTests
{
    [Fact]
    public void Diff_degisen_alanlari_listeler()
    {
        var before = new UserFieldChangeAudit.Snapshot(
            "Ayşe", "Yılmaz", "a@x.com", "0532", UserRole.User, 1, 2, true);
        var after = new UserFieldChangeAudit.Snapshot(
            "Ayşe", "Demir", "a@x.com", null, UserRole.Agent, 1, null, false);

        var changes = UserFieldChangeAudit.Diff(before, after, passwordChanged: true);

        Assert.Contains(changes, c => c.Field == "Soyad" && c.NewValue == "Demir");
        Assert.Contains(changes, c => c.Field == "Telefon" && c.NewValue == "");
        Assert.Contains(changes, c => c.Field == "Rol");
        Assert.Contains(changes, c => c.Field == "Ekip" && c.NewValue == "-");
        Assert.Contains(changes, c => c.Field == "Aktif" && c.NewValue == "Hayır");
        Assert.Contains(changes, c => c.Field == "Şifre");
    }

    [Fact]
    public void Diff_degisiklik_yoksa_bos()
    {
        var s = new UserFieldChangeAudit.Snapshot(
            "A", "B", "c@x", null, UserRole.Admin, null, null, true);
        Assert.Empty(UserFieldChangeAudit.Diff(s, s));
    }

    [Fact]
    public void FormatDetail_hedef_ve_alanlari_yazar()
    {
        var detail = UserFieldChangeAudit.FormatDetail(7, "Ali Veli",
            new[] { new UserFieldChangeAudit.Change("Rol", "Kullanıcı", "Destek") });
        Assert.Contains("#7 Ali Veli", detail);
        Assert.Contains("Rol: Kullanıcı → Destek", detail);
    }
}
