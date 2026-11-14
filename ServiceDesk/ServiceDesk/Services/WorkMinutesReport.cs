using System.Globalization;

namespace ServiceDesk.Services;

// Personel efor (work minutes) raporu — TicketComment.WorkMinutes üzerinden.
// Notlara girilen süreler yazar bazında toplanır (ISO 9001 md. 9.1.3).
public static class WorkMinutesReportPolicy
{
    public sealed record Entry(int AuthorId, int TicketId, int Minutes);

    public sealed record Row(
        int UserId,
        string FullName,
        string? TeamName,
        int EntryCount,
        int TicketCount,
        int TotalMinutes)
    {
        public double TotalHours => Math.Round(TotalMinutes / 60.0, 1);
    }

    public sealed record Summary(int People, int Entries, int Tickets, int TotalMinutes)
    {
        public double TotalHours => Math.Round(TotalMinutes / 60.0, 1);
    }

    public static List<Row> Summarize(
        IEnumerable<Entry> entries,
        IReadOnlyDictionary<int, (string FullName, string? TeamName)> users)
    {
        return entries
            .GroupBy(e => e.AuthorId)
            .Select(g =>
            {
                users.TryGetValue(g.Key, out var u);
                return new Row(
                    g.Key,
                    string.IsNullOrWhiteSpace(u.FullName) ? $"#{g.Key}" : u.FullName,
                    u.TeamName,
                    g.Count(),
                    g.Select(x => x.TicketId).Distinct().Count(),
                    g.Sum(x => x.Minutes));
            })
            .OrderByDescending(r => r.TotalMinutes)
            .ThenBy(r => r.FullName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    // distinctTickets: dönemdeki benzersiz talep sayısı (girişlerden).
    public static Summary Totals(IReadOnlyList<Row> rows, int distinctTickets) =>
        new(rows.Count,
            rows.Sum(r => r.EntryCount),
            distinctTickets,
            rows.Sum(r => r.TotalMinutes));

    public static string FormatHours(int minutes) =>
        (minutes / 60.0).ToString("0.0", CultureInfo.InvariantCulture);
}
