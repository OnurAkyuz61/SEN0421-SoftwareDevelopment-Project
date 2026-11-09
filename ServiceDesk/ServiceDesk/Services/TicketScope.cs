using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Destek personelinin hangi talepleri görebileceği.
// Ekibi tanımlı destek personeli yalnızca kendi ekibine düşen talepleri ve
// kendisine atanmış talepleri görür. Yönetici ve ekibi olmayan destek
// personeli (ör. genel havuz) tüm talepleri görür.
public static class TicketScope
{
    public static bool IsRestricted(AppUser user) => user.Role == UserRole.Agent && user.TeamId != null;

    public static IQueryable<Ticket> VisibleTo(this IQueryable<Ticket> query, AppUser user)
    {
        if (!IsRestricted(user)) return query;
        var teamId = user.TeamId;
        var userId = user.Id;
        return query.Where(t => t.AssignedTeamId == teamId || t.AssigneeId == userId);
    }

    public static bool CanSee(AppUser user, Ticket ticket) =>
        !IsRestricted(user) || ticket.AssignedTeamId == user.TeamId || ticket.AssigneeId == user.Id;
}
