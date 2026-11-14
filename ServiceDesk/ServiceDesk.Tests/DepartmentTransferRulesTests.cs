using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class DepartmentTransferRulesTests
{
    [Theory]
    [InlineData(null, 1, false)]   // ilk kez ekibe katılma
    [InlineData(1, 1, false)]      // ekip aynı
    [InlineData(1, 2, true)]       // ekip değişimi
    [InlineData(1, null, true)]    // ekipten çıkış
    [InlineData(null, null, false)]
    public void RequiresReassignment_ekip_degisimini_dogru_bulur(int? eski, int? yeni, bool beklenen)
    {
        Assert.Equal(beklenen, DepartmentTransferRules.RequiresReassignment(eski, yeni));
    }

    [Theory]
    [InlineData(5, 1, 5)]   // talebin ekibi korunur
    [InlineData(null, 1, 1)] // yoksa eski ekibe düşer
    [InlineData(null, null, null)]
    public void QueueTeamId_kuyrugu_dogru_secer(int? talepEkibi, int? eskiEkip, int? beklenen)
    {
        Assert.Equal(beklenen, DepartmentTransferRules.QueueTeamId(talepEkibi, eskiEkip));
    }
}
