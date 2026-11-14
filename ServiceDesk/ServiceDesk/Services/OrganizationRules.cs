namespace ServiceDesk.Services;

// Kullanıcı birim ↔ ekip tutarlılığı.
//
// Ekip her zaman bir birime bağlıdır. Kullanıcının birimi değişince eski ekip
// geçersiz kalır; ya ekip temizlenmeli ya da yeni birime ait bir ekip seçilmeli.
// Saf sınıf: birim testi yazılabiliyor.
public static class OrganizationRules
{
    public record OrgAssignment(int? DepartmentId, int? TeamId, string? Error);

    // teamDepartmentId: seçilen ekibin DepartmentId'si (ekip yoksa null).
    // teamFound: TeamId doluysa kaydın gerçekten var olup olmadığı.
    public static OrgAssignment Resolve(int? departmentId, int? teamId,
                                        bool teamFound, int? teamDepartmentId)
    {
        if (teamId == null)
            return new OrgAssignment(departmentId, null, null);

        if (!teamFound)
            return new OrgAssignment(departmentId, teamId, "Geçerli bir ekip seçin.");

        // Birim boş, ekip dolu → birimi ekipten doldur (ekip birimi belirler)
        if (departmentId == null)
            return new OrgAssignment(teamDepartmentId, teamId, null);

        if (teamDepartmentId != departmentId)
            return new OrgAssignment(departmentId, teamId,
                "Seçilen ekip bu birime bağlı değil. Birim değiştiyse ekibi yeniden seçin veya temizleyin.");

        return new OrgAssignment(departmentId, teamId, null);
    }

    // Birim değişince mevcut ekip hâlâ uyuyor mu? UI / otomatik temizleme için.
    public static bool TeamMatchesDepartment(int? departmentId, int? teamDepartmentId) =>
        departmentId == null || teamDepartmentId == null || departmentId == teamDepartmentId;
}
