using System.Globalization;
using System.Text;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Talep listesi CSV dışa aktarımı (Türkçe Excel: ; ayırıcı + UTF-8 BOM).
public static class TicketListCsv
{
    public const char Separator = ';';
    public const int MaxRows = 50_000;

    public static readonly string[] Headers =
    {
        "No", "Konu", "Tür", "Öncelik", "Durum", "Kategori",
        "Talep Eden", "Atanan", "Ekip", "Oluşturma", "SLA Bitiş",
        "SLA İhlal", "Etiketler"
    };

    public sealed record Row(
        string Number,
        string Title,
        string Type,
        string Priority,
        string Status,
        string Category,
        string Requester,
        string Assignee,
        string Team,
        string CreatedAt,
        string SlaDueAt,
        string SlaBreached,
        string Tags);

    public static Row FromTicket(Ticket t) => new(
        Ui.TicketNo(t),
        t.Title,
        Ui.TypeText(t.Type),
        Ui.PriorityText(t.Priority),
        Ui.StatusText(t.Status),
        t.Category == null ? "" : Ui.CategoryText(t.Category),
        t.RequesterName,
        t.Assignee?.FullName ?? "",
        t.AssignedTeam?.Name ?? "",
        FormatLocal(t.CreatedAt),
        FormatLocal(t.SlaDueAt),
        t.IsSlaBreached ? "Evet" : "Hayır",
        t.Tags ?? "");

    public static string Build(IEnumerable<Row> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(Separator, Headers.Select(Escape)));
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(Separator, new[]
            {
                r.Number, r.Title, r.Type, r.Priority, r.Status, r.Category,
                r.Requester, r.Assignee, r.Team, r.CreatedAt, r.SlaDueAt,
                r.SlaBreached, r.Tags
            }.Select(Escape)));
        }
        return sb.ToString();
    }

    public static byte[] ToUtf8BomBytes(string csv) =>
        Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();

    // ReportsController.Csv ile aynı: formül enjeksiyonu + ; / tırnak kaçışı
    public static string Escape(string v)
    {
        if (v.Length > 0 && "=+-@".Contains(v[0])
            && !double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            v = "'" + v;

        return v.IndexOfAny(new[] { Separator, '"', '\n', '\r' }) >= 0
            ? $"\"{v.Replace("\"", "\"\"")}\""
            : v;
    }

    private static string FormatLocal(DateTime utc) =>
        AppTime.ToLocal(utc).ToString("dd.MM.yyyy HH:mm");
}
