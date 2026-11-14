using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Pasifleştirmede açık taleplerin otomatik bırakılması.
//
// Atanan (AssigneeId) temizlenir; ekip kuyruğu (AssignedTeamId) korunur —
// talepler ekip havuzunda kalır, Claim / manuel atama ile alınabilir.
// Çağıran SaveChanges yapar.
public static class InactiveUserTicketReleasePolicy
{
    public static bool ShouldRelease(bool wasActive, bool isActive) => wasActive && !isActive;
}

public class InactiveUserTicketRelease
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public InactiveUserTicketRelease(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public sealed record Result(int Released, IReadOnlyList<int> TicketIds);

    public async Task<Result> ApplyAsync(int userId, int byUserId, string userDisplayName)
    {
        var tickets = await _db.Tickets
            .Where(t => t.AssigneeId == userId
                && (t.Status == TicketStatus.New
                    || t.Status == TicketStatus.InProgress
                    || t.Status == TicketStatus.WaitingForCustomer))
            .ToListAsync();

        if (tickets.Count == 0)
            return new Result(0, Array.Empty<int>());

        var ids = new List<int>(tickets.Count);
        foreach (var ticket in tickets)
        {
            ticket.AssigneeId = null;
            ids.Add(ticket.Id);
        }

        _audit.Queue(AuditAction.InactiveUserTicketsReleased, byUserId,
            detail: $"{userDisplayName} pasifleştirildi: {ids.Count} açık talep atamasız bırakıldı");

        return new Result(ids.Count, ids);
    }
}
