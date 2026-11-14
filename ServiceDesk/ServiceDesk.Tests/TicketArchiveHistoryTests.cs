using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class TicketArchiveHistoryTests
{
    [Fact]
    public void Archived_nedeni_newvalue_icerir()
    {
        var h = TicketArchiveHistory.Archived(5, 1, "Mükerrer kayıt", DateTime.UtcNow);
        Assert.Equal(TicketArchiveHistory.FieldName, h.Field);
        Assert.Equal("Aktif", h.OldValue);
        Assert.StartsWith("Arşivlendi —", h.NewValue);
        Assert.Contains("Mükerrer", h.NewValue);
        Assert.True(TicketArchiveHistory.IsArchiveEvent(h));
    }

    [Fact]
    public void Restored_onceki_nedeni_oldvalue_de_tutar()
    {
        var h = TicketArchiveHistory.Restored(5, 1, "Test nedeni", DateTime.UtcNow);
        Assert.Equal("Geri yüklendi", h.NewValue);
        Assert.Contains("Test nedeni", h.OldValue);
    }

    [Fact]
    public void Cut_100_karakteri_asimaz()
    {
        var longReason = new string('x', 200);
        var h = TicketArchiveHistory.Archived(1, 1, longReason, DateTime.UtcNow);
        Assert.True(h.NewValue!.Length <= 100);
        Assert.EndsWith("…", h.NewValue);
    }
}
