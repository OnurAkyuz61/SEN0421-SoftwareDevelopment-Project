using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class WorkMinutesReportTests
{
    [Fact]
    public void Summarize_yazara_gore_toplar_ve_siralar()
    {
        var entries = new[]
        {
            new WorkMinutesReportPolicy.Entry(1, 10, 30),
            new WorkMinutesReportPolicy.Entry(1, 10, 15),
            new WorkMinutesReportPolicy.Entry(1, 11, 20),
            new WorkMinutesReportPolicy.Entry(2, 10, 100),
        };
        var users = new Dictionary<int, (string, string?)>
        {
            [1] = ("Ali Veli", "IT"),
            [2] = ("Zeynep", "IT"),
        };

        var rows = WorkMinutesReportPolicy.Summarize(entries, users);

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0].UserId); // 100 dk
        Assert.Equal(100, rows[0].TotalMinutes);
        Assert.Equal(1, rows[1].UserId);
        Assert.Equal(65, rows[1].TotalMinutes);
        Assert.Equal(3, rows[1].EntryCount);
        Assert.Equal(2, rows[1].TicketCount);
        Assert.Equal(1.1, rows[1].TotalHours);
    }

    [Fact]
    public void Totals_ozeti_dogru()
    {
        var rows = new[]
        {
            new WorkMinutesReportPolicy.Row(1, "A", null, 2, 1, 30),
            new WorkMinutesReportPolicy.Row(2, "B", "X", 1, 1, 90),
        };
        var s = WorkMinutesReportPolicy.Totals(rows, distinctTickets: 2);
        Assert.Equal(2, s.People);
        Assert.Equal(3, s.Entries);
        Assert.Equal(2, s.Tickets);
        Assert.Equal(120, s.TotalMinutes);
        Assert.Equal(2.0, s.TotalHours);
    }
}
