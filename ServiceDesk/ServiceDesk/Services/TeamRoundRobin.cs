using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Ekip bazlı otomatik atama: ekibin round-robin kuralı açıksa yeni talep
// ekipteki aktif destek personeline sırayla dağıtılır.
public class TeamRoundRobin
{
    private readonly AppDbContext _db;
    public TeamRoundRobin(AppDbContext db) => _db = db;

    // Atama yapılırsa atanan kişinin Id'si döner. Değişiklikler kaydedilmez;
    // talebi açan akış SaveChanges ile birlikte yazar.
    public async Task<int?> AssignAsync(Ticket ticket)
    {
        if (ticket.AssignedTeamId == null || ticket.AssigneeId != null) return null;

        var team = await _db.Teams.FirstOrDefaultAsync(t =>
            t.Id == ticket.AssignedTeamId && t.IsActive && t.RoundRobinEnabled);
        if (team == null) return null;

        var members = await _db.Users.AsNoTracking()
            .Where(u => u.TeamId == team.Id && u.IsActive && u.Role != UserRole.User)
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .ToListAsync();

        var next = RoundRobinPolicy.Next(members, team.LastAssignedUserId);
        if (next == null) return null;

        ticket.AssigneeId = next;
        team.LastAssignedUserId = next;
        return next;
    }
}

// Saf sıra seçimi (birim testi yazılabilsin diye ayrı)
public static class RoundRobinPolicy
{
    // Son atanandan sonraki ilk kişi; liste sonuna gelindiyse baştan
    public static int? Next(IReadOnlyList<int> orderedMemberIds, int? lastAssignedId)
    {
        if (orderedMemberIds.Count == 0) return null;
        if (lastAssignedId != null)
            foreach (var id in orderedMemberIds)
                if (id > lastAssignedId) return id;
        return orderedMemberIds[0];
    }
}
