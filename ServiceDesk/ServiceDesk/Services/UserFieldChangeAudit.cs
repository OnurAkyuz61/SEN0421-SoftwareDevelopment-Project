using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Kullanıcı alan değişikliklerini AuditLogs detay metnine çevirir.
// Şifre özeti yazılmaz; yalnızca "değiştirildi" notu düşülür.
public static class UserFieldChangeAudit
{
    public sealed record Snapshot(
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        UserRole Role,
        int? DepartmentId,
        int? TeamId,
        bool IsActive);

    public sealed record Change(string Field, string OldValue, string NewValue);

    public static Snapshot Capture(AppUser u) => new(
        u.FirstName, u.LastName, u.Email, u.PhoneNumber,
        u.Role, u.DepartmentId, u.TeamId, u.IsActive);

    public static IReadOnlyList<Change> Diff(Snapshot before, Snapshot after, bool passwordChanged = false)
    {
        var list = new List<Change>();
        void Add(string field, string? oldV, string? newV)
        {
            oldV ??= "";
            newV ??= "";
            if (oldV != newV) list.Add(new Change(field, oldV, newV));
        }

        Add("Ad", before.FirstName, after.FirstName);
        Add("Soyad", before.LastName, after.LastName);
        Add("E-posta", before.Email, after.Email);
        Add("Telefon", before.PhoneNumber, after.PhoneNumber);
        Add("Rol", Ui.RoleText(before.Role), Ui.RoleText(after.Role));
        Add("Birim", IdText(before.DepartmentId), IdText(after.DepartmentId));
        Add("Ekip", IdText(before.TeamId), IdText(after.TeamId));
        Add("Aktif", before.IsActive ? "Evet" : "Hayır", after.IsActive ? "Evet" : "Hayır");
        if (passwordChanged)
            list.Add(new Change("Şifre", "•", "değiştirildi"));

        return list;
    }

    // AuditLog.Detail: "Hedef #7 Ad Soyad: Alan: eski → yeni; ..."
    public static string? FormatDetail(int targetUserId, string targetDisplayName,
                                      IReadOnlyList<Change> changes)
    {
        if (changes.Count == 0) return null;
        var parts = changes.Select(c => $"{c.Field}: {Cut(c.OldValue)} → {Cut(c.NewValue)}");
        var body = string.Join("; ", parts);
        var head = $"#{targetUserId} {targetDisplayName}: ";
        var maxBody = 1000 - head.Length;
        if (maxBody < 20) return head.TrimEnd(':', ' ');
        if (body.Length > maxBody) body = body[..(maxBody - 1)] + "…";
        return head + body;
    }

    private static string IdText(int? id) => id == null ? "-" : $"#{id}";

    private static string Cut(string s) => s.Length <= 80 ? s : s[..77] + "…";
}
