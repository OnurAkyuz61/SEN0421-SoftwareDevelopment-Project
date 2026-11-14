using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class TicketListCsvTests
{
    [Theory]
    [InlineData("normal", "normal")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("=CMD", "'=CMD")]
    [InlineData("-1.5", "-1.5")]
    public void Escape_formül_ve_ayiriciyi_korur(string giris, string beklenen)
    {
        Assert.Equal(beklenen, TicketListCsv.Escape(giris));
    }

    [Fact]
    public void Build_baslik_ve_satir_uret()
    {
        var csv = TicketListCsv.Build(new[]
        {
            new TicketListCsv.Row(
                "INC-1", "Yazıcı; arıza", "Olay", "Yüksek", "Yeni", "Donanım",
                "Ali", "Ayşe", "IT", "01.01.2026 10:00", "02.01.2026 10:00",
                "Hayır", "yazici")
        });

        Assert.Contains("No;Konu;Tür", csv);
        Assert.Contains("INC-1", csv);
        Assert.Contains("\"Yazıcı; arıza\"", csv);
        Assert.Contains("Ayşe", csv);
    }

    [Fact]
    public void ToUtf8BomBytes_bom_ekler()
    {
        var bytes = TicketListCsv.ToUtf8BomBytes("a;b");
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }
}
