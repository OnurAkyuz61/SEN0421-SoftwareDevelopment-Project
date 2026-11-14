using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Yönetici kritik işlemleri: pasifleştirme, rol, talep arşiv/geri yükleme.
// Genel alan denetiminden (UserFieldsChanged) ayrı AuditAction ile yazılır.
public static class AdminCriticalAudit
{
    public static readonly AuditAction[] CriticalActions =
    {
        AuditAction.AdminUserDeactivated,
        AuditAction.AdminUserActivated,
        AuditAction.AdminUserRoleChanged,
        AuditAction.TicketArchived,
        AuditAction.TicketRestored,
        AuditAction.UserAnonymized,
        AuditAction.ForceLogout
    };

    public static bool IsCritical(AuditAction a) => CriticalActions.Contains(a);

    public static void QueueUserActive(AuditService audit, int byUserId, string byEmail,
                                      AppUser target, bool nowActive)
    {
        audit.Queue(
            nowActive ? AuditAction.AdminUserActivated : AuditAction.AdminUserDeactivated,
            byUserId, byEmail,
            detail: $"#{target.Id} {target.FullName} ({target.Email}) → {(nowActive ? "aktif" : "pasif")}");
    }

    public static void QueueUserRole(AuditService audit, int byUserId, string byEmail,
                                    AppUser target, UserRole oldRole, UserRole newRole)
    {
        if (oldRole == newRole) return;
        audit.Queue(AuditAction.AdminUserRoleChanged, byUserId, byEmail,
            detail: $"#{target.Id} {target.FullName}: {Ui.RoleText(oldRole)} → {Ui.RoleText(newRole)}");
    }

    public static void QueueTicketArchived(AuditService audit, int byUserId, string byEmail,
                                          Ticket ticket, string reason)
    {
        audit.Queue(AuditAction.TicketArchived, byUserId, byEmail,
            detail: $"{Ui.TicketNo(ticket)} arşivlendi. Neden: {reason}",
            ticketId: ticket.Id);
    }

    public static void QueueTicketRestored(AuditService audit, int byUserId, string byEmail,
                                          Ticket ticket)
    {
        audit.Queue(AuditAction.TicketRestored, byUserId, byEmail,
            detail: $"{Ui.TicketNo(ticket)} arşivden geri yüklendi.",
            ticketId: ticket.Id);
    }
}
