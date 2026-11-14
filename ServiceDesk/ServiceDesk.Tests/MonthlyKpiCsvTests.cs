using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class MonthlyKpiCsvTests
{
    [Fact]
    public void Build_baslik_ve_donem_satiri_uret()
    {
        var snap = new KpiSnapshot
        {
            Period = KpiPeriod.Monthly,
            PeriodStart = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            TotalTickets = 10,
            ResolvedTickets = 8,
            BreachedTickets = 1,
            ReopenCount = 2,
            FirstResponseBreachedTickets = 0,
            FirstResponseComplianceRate = 100m,
            SlaComplianceRate = 90m,
            AvgResolutionHours = 4.5m,
            AvgFirstResponseHours = 1.2m,
            CsatAverage = 4.25m,
            SurveyResponseCount = 5,
            CsatResponseRate = 62.5m,
            LowCsatCount = 1
        };

        var csv = MonthlyKpiCsv.Build(new[] { MonthlyKpiCsv.From(snap) });

        Assert.StartsWith("Dönem;Başlangıç;Bitiş;Birim", csv);
        Assert.Contains("Kurum geneli", csv);
        Assert.Contains("10", csv);
        Assert.Contains("4.25", csv);
        Assert.Contains("90.0", csv);
    }

    [Fact]
    public void From_birim_adini_yazar()
    {
        var snap = new KpiSnapshot
        {
            PeriodStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            Department = new Department { Name = "Bilgi İşlem" }
        };

        var row = MonthlyKpiCsv.From(snap);
        Assert.Equal("Bilgi İşlem", row.Department);
    }
}
