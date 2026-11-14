using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Destek personeli iş yükü sıralaması (açık talep sayısına göre).
// En az yüklü başa gelir — atama / eskalasyon seçiminde kullanılır.
public static class AgentWorkloadRankingPolicy
{
    public sealed record Row(
        int UserId,
        string FullName,
        string Email,
        UserRole Role,
        int? TeamId,
        string? TeamName,
        int OpenTickets);

    public sealed record Ranked(
        int Rank,
        int UserId,
        string FullName,
        string Email,
        UserRole Role,
        int? TeamId,
        string? TeamName,
        int OpenTickets);

    // Açık talep artan, eşitlikte ada göre. Sıra 1'den başlar.
    public static IReadOnlyList<Ranked> Rank(IEnumerable<Row> rows) =>
        rows
            .OrderBy(r => r.OpenTickets)
            .ThenBy(r => r.FullName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(r => r.UserId)
            .Select((r, i) => new Ranked(
                i + 1, r.UserId, r.FullName, r.Email, r.Role,
                r.TeamId, r.TeamName, r.OpenTickets))
            .ToList();
}

public class AgentWorkloadRanking
{
    private readonly AppDbContext _db;
    public AgentWorkloadRanking(AppDbContext db) => _db = db;

    // teamId doluysa yalnızca o ekipteki aktif personel.
    public async Task<IReadOnlyList<AgentWorkloadRankingPolicy.Ranked>> ListAsync(int? teamId = null)
    {
        var staffQuery = _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Role != UserRole.User);
        if (teamId != null)
            staffQuery = staffQuery.Where(u => u.TeamId == teamId);

        var staff = await staffQuery
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Role,
                u.TeamId,
                TeamName = u.Team != null ? u.Team.Name : null
            })
            .ToListAsync();

        if (staff.Count == 0)
            return Array.Empty<AgentWorkloadRankingPolicy.Ranked>();

        var ids = staff.Select(s => s.Id).ToList();
        var counts = await _db.Tickets.AsNoTracking()
            .Where(t => t.AssigneeId != null && ids.Contains(t.AssigneeId.Value)
                && (t.Status == TicketStatus.New
                    || t.Status == TicketStatus.InProgress
                    || t.Status == TicketStatus.WaitingForCustomer))
            .GroupBy(t => t.AssigneeId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        var rows = staff.Select(s => new AgentWorkloadRankingPolicy.Row(
            s.Id,
            $"{s.FirstName} {s.LastName}".Trim(),
            s.Email,
            s.Role,
            s.TeamId,
            s.TeamName,
            counts.GetValueOrDefault(s.Id)));

        return AgentWorkloadRankingPolicy.Rank(rows);
    }
}
