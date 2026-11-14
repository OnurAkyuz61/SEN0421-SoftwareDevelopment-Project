using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Birim bazlı SLA uyum özeti (talep sahibinin DepartmentId'sine göre).
// KpiService ile aynı kapsam: dönemde AÇILAN talepler.
public static class DepartmentSlaCompliancePolicy
{
    public sealed record TicketFact(
        int? DepartmentId,
        bool IsSlaBreached,
        bool HasFirstResponseDue,
        bool IsFirstResponseBreached);

    public sealed record Row(
        int? DepartmentId,
        string DepartmentName,
        int TotalTickets,
        int BreachedTickets,
        decimal SlaComplianceRate,
        int FirstResponseMeasured,
        int FirstResponseBreached,
        decimal FirstResponseComplianceRate);

    public static decimal ComplianceRate(int total, int breached) =>
        total == 0 ? 100m : Math.Round(100m * (total - breached) / total, 1);

    public static IReadOnlyList<Row> Summarize(
        IEnumerable<TicketFact> tickets,
        IReadOnlyDictionary<int, string> departmentNames)
    {
        return tickets
            .GroupBy(t => t.DepartmentId)
            .Select(g =>
            {
                var total = g.Count();
                var breached = g.Count(x => x.IsSlaBreached);
                var fr = g.Where(x => x.HasFirstResponseDue).ToList();
                var frBreached = fr.Count(x => x.IsFirstResponseBreached);
                var name = g.Key == null
                    ? "Birimsiz"
                    : departmentNames.GetValueOrDefault(g.Key.Value, $"#{g.Key}");
                return new Row(
                    g.Key,
                    name,
                    total,
                    breached,
                    ComplianceRate(total, breached),
                    fr.Count,
                    frBreached,
                    ComplianceRate(fr.Count, frBreached));
            })
            .OrderBy(r => r.DepartmentName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}

public class DepartmentSlaCompliance
{
    private readonly AppDbContext _db;
    public DepartmentSlaCompliance(AppDbContext db) => _db = db;

    // from/to yerel gün; UTC'ye çevrilir. from > to ise yer değiştirilir.
    public async Task<(DateTime FromLocal, DateTime ToLocal, IReadOnlyList<DepartmentSlaCompliancePolicy.Row> Rows)>
        ReportAsync(DateTime? fromLocal, DateTime? toLocal)
    {
        var from = (fromLocal ?? AppTime.LocalToday.AddDays(-30)).Date;
        var to = (toLocal ?? AppTime.LocalToday).Date;
        if (from > to) (from, to) = (to, from);

        var fromUtc = AppTime.ToUtc(from);
        var toUtc = AppTime.ToUtc(to.AddDays(1));

        var facts = await _db.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= fromUtc && t.CreatedAt < toUtc)
            .Select(t => new DepartmentSlaCompliancePolicy.TicketFact(
                t.Requester!.DepartmentId,
                t.IsSlaBreached,
                t.FirstResponseDueAt != null,
                t.IsFirstResponseBreached))
            .ToListAsync();

        var deptIds = facts.Where(f => f.DepartmentId != null)
            .Select(f => f.DepartmentId!.Value).Distinct().ToList();
        var names = await _db.Departments.AsNoTracking()
            .Where(d => deptIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name);

        return (from, to, DepartmentSlaCompliancePolicy.Summarize(facts, names));
    }
}
