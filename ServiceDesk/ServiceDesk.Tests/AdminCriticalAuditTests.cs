using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class AdminCriticalAuditTests
{
    [Theory]
    [InlineData(AuditAction.AdminUserDeactivated, true)]
    [InlineData(AuditAction.TicketArchived, true)]
    [InlineData(AuditAction.UserFieldsChanged, false)]
    [InlineData(AuditAction.CatalogChanged, false)]
    [InlineData(AuditAction.ForceLogout, true)]
    public void IsCritical_dogru_ayirir(AuditAction action, bool beklenen)
    {
        Assert.Equal(beklenen, AdminCriticalAudit.IsCritical(action));
    }
}
