namespace ServiceDesk.Services;

// Birim / ekip transferinde açık taleplerin yeniden atanma kuralları.
//
// Ekip değişince (veya ekip temizlenince) kişiye atanmış açık talepler eski
// ekip kuyruğunda kalır ve başka bir üyeye devredilir. İlk kez ekibe katılma
// (eski ekip yok) kişisel atamaları bozmaz.
public static class DepartmentTransferRules
{
    // Eski ekip varken TeamId değiştiyse yeniden atama gerekir.
    public static bool RequiresReassignment(int? oldTeamId, int? newTeamId) =>
        oldTeamId != null && oldTeamId != newTeamId;

    // Talebin kalacağı kuyruk: mevcut AssignedTeamId; yoksa ayrıldığı ekip.
    public static int? QueueTeamId(int? ticketAssignedTeamId, int? oldTeamId) =>
        ticketAssignedTeamId ?? oldTeamId;
}
