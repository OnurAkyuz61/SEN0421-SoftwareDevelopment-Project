using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Soft-delete (arşiv) ve restore olaylarının talep geçmişine net yazımı.
// Field adı "Arşiv durumu" — diğer alan değişikliklerinden ayırt edilir;
// NewValue içinde arşiv nedeni kısaltılarak saklanır (kolon max 100).
public static class TicketArchiveHistory
{
    public const string FieldName = "Arşiv durumu";
    public const string ActiveLabel = "Aktif";
    public const string ArchivedLabel = "Arşivlendi";
    public const string RestoredLabel = "Geri yüklendi";

    public static TicketHistory Archived(int ticketId, int byUserId, string reason, DateTime atUtc) =>
        new()
        {
            TicketId = ticketId,
            Field = FieldName,
            OldValue = ActiveLabel,
            NewValue = Cut($"{ArchivedLabel} — {reason.Trim()}"),
            ChangedById = byUserId,
            ChangedAt = atUtc
        };

    public static TicketHistory Restored(int ticketId, int byUserId, string? previousReason, DateTime atUtc) =>
        new()
        {
            TicketId = ticketId,
            Field = FieldName,
            OldValue = string.IsNullOrWhiteSpace(previousReason)
                ? ArchivedLabel
                : Cut($"{ArchivedLabel} — {previousReason.Trim()}"),
            NewValue = RestoredLabel,
            ChangedById = byUserId,
            ChangedAt = atUtc
        };

    public static bool IsArchiveEvent(TicketHistory h) =>
        string.Equals(h.Field, FieldName, StringComparison.Ordinal);

    // TicketHistory.NewValue / OldValue: StringLength(100)
    public static string Cut(string s) =>
        s.Length <= 100 ? s : s[..97] + "…";
}
