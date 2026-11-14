using System.Globalization;
using System.Text;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Aylık KPI özeti CSV (md. 9.1.1). Türkçe Excel: ; + UTF-8 BOM.
public static class MonthlyKpiCsv
{
    public const int DefaultMonths = 24;

    public static readonly string[] Headers =
    {
        "Dönem", "Başlangıç", "Bitiş", "Birim",
        "Toplam talep", "Çözülen", "SLA ihlal", "Yeniden açılma",
        "İlk yanıt ihlal", "İlk yanıt SLA (%)", "Çözüm SLA (%)",
        "Ort. çözüm (saat)", "Ort. ilk yanıt (saat)",
        "Memnuniyet", "Anket yanıtı", "Yanıt oranı (%)", "Düşük puanlı"
    };

    public sealed record Row(
        string PeriodLabel,
        string PeriodStart,
        string PeriodEnd,
        string Department,
        string TotalTickets,
        string ResolvedTickets,
        string BreachedTickets,
        string ReopenCount,
        string FirstResponseBreached,
        string FirstResponseCompliance,
        string SlaCompliance,
        string AvgResolutionHours,
        string AvgFirstResponseHours,
        string CsatAverage,
        string SurveyResponseCount,
        string CsatResponseRate,
        string LowCsatCount);

    public static Row From(KpiSnapshot k)
    {
        var startLocal = AppTime.ToLocal(k.PeriodStart);
        return new Row(
            startLocal.ToString("MMMM yyyy"),
            startLocal.ToString("dd.MM.yyyy"),
            AppTime.ToLocal(k.PeriodEnd).ToString("dd.MM.yyyy"),
            k.Department?.Name ?? "Kurum geneli",
            N(k.TotalTickets),
            N(k.ResolvedTickets),
            N(k.BreachedTickets),
            N(k.ReopenCount),
            N(k.FirstResponseBreachedTickets),
            D(k.FirstResponseComplianceRate),
            D(k.SlaComplianceRate),
            D(k.AvgResolutionHours),
            D(k.AvgFirstResponseHours),
            k.SurveyResponseCount == 0 ? "" : D(k.CsatAverage, "0.00"),
            N(k.SurveyResponseCount),
            D(k.CsatResponseRate),
            N(k.LowCsatCount));
    }

    public static string Build(IEnumerable<Row> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(TicketListCsv.Separator, Headers.Select(TicketListCsv.Escape)));
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(TicketListCsv.Separator, new[]
            {
                r.PeriodLabel, r.PeriodStart, r.PeriodEnd, r.Department,
                r.TotalTickets, r.ResolvedTickets, r.BreachedTickets, r.ReopenCount,
                r.FirstResponseBreached, r.FirstResponseCompliance, r.SlaCompliance,
                r.AvgResolutionHours, r.AvgFirstResponseHours,
                r.CsatAverage, r.SurveyResponseCount, r.CsatResponseRate, r.LowCsatCount
            }.Select(TicketListCsv.Escape)));
        }
        return sb.ToString();
    }

    private static string N(int v) => v.ToString(CultureInfo.InvariantCulture);
    private static string D(decimal v, string format = "0.0") =>
        v.ToString(format, CultureInfo.InvariantCulture);
}
