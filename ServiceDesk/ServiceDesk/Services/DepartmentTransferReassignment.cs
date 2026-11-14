using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Services;

// Kullanıcı ekip değiştirince (departman transferi) açık taleplerini eski
// ekip kuyruğunda yeniden atar. Çağıran SaveChanges yapar; bu sınıf kullanıcı
// TeamId güncellendikten SONRA çağrılmalıdır (üye listesinden düşmüş olsun).
public class DepartmentTransferReassignment
{
    private readonly AppDbContext _db;
    private readonly TeamRoundRobin _roundRobin;
    private readonly NotificationService _notify;
    private readonly AuditService _audit;

    public DepartmentTransferReassignment(AppDbContext db, TeamRoundRobin roundRobin,
                                          NotificationService notify, AuditService audit)
    {
        _db = db;
        _roundRobin = roundRobin;
        _notify = notify;
        _audit = audit;
    }

    public sealed record Result(int Reassigned, int Unassigned, IReadOnlyList<int> TicketIds);

    public async Task<Result> ApplyAsync(int userId, int? oldTeamId, int? newTeamId,
                                         int byUserId, string userDisplayName)
    {
        if (!DepartmentTransferRules.RequiresReassignment(oldTeamId, newTeamId))
            return new Result(0, 0, Array.Empty<int>());

        var tickets = await _db.Tickets
            .Where(t => t.AssigneeId == userId
                && (t.Status == TicketStatus.New
                    || t.Status == TicketStatus.InProgress
                    || t.Status == TicketStatus.WaitingForCustomer))
            .ToListAsync();

        if (tickets.Count == 0)
            return new Result(0, 0, Array.Empty<int>());

        var reassigned = 0;
        var unassigned = 0;
        var ids = new List<int>(tickets.Count);

        foreach (var ticket in tickets)
        {
            var queueTeamId = DepartmentTransferRules.QueueTeamId(ticket.AssignedTeamId, oldTeamId);
            ticket.AssigneeId = null;
            if (queueTeamId != null)
                ticket.AssignedTeamId = queueTeamId;

            int? nextId = null;
            if (queueTeamId != null)
            {
                nextId = await _roundRobin.AssignAsync(ticket);
                if (nextId == null)
                    nextId = await AssignLeastLoadedAsync(ticket, queueTeamId.Value, excludeUserId: userId);
            }

            ids.Add(ticket.Id);

            if (nextId is int assignedId)
            {
                reassigned++;
                _notify.Queue(assignedId, NotificationType.Assigned,
                    $"Transfer nedeniyle size atandı: {Ui.TicketNo(ticket)} {ticket.Title}",
                    $"{userDisplayName} ekip değiştirdi; açık talep size devredildi.",
                    ticket.Id, EmailTemplates.Assignment);
            }
            else
            {
                unassigned++;
            }
        }

        _audit.Queue(AuditAction.DepartmentTransferReassigned, byUserId,
            detail: $"{userDisplayName}: {reassigned} yeniden atandı, {unassigned} atamasız bırakıldı " +
                    $"(eski ekip #{oldTeamId} → {(newTeamId == null ? "yok" : "#" + newTeamId)})");

        return new Result(reassigned, unassigned, ids);
    }

    // Round-robin kapalıysa veya sıra üretemezse: ekipten en az yüklü aktif personel.
    private async Task<int?> AssignLeastLoadedAsync(Ticket ticket, int teamId, int excludeUserId)
    {
        var candidate = await _db.Users
            .Where(u => u.TeamId == teamId && u.IsActive && u.Role != UserRole.User && u.Id != excludeUserId)
            .OrderBy(u => _db.Tickets.Count(t => t.AssigneeId == u.Id
                && (t.Status == TicketStatus.New
                    || t.Status == TicketStatus.InProgress
                    || t.Status == TicketStatus.WaitingForCustomer)))
            .ThenBy(u => u.Id)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();

        if (candidate == null) return null;

        ticket.AssigneeId = candidate;
        return candidate;
    }
}
