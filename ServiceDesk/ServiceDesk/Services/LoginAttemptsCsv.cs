using System.Text;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Giriş denemesi denetim izi CSV (Excel: ; + UTF-8 BOM).
public static class LoginAttemptsCsv
{
    public const int MaxRows = 50_000;

    public static readonly string[] Headers =
    {
        "Zaman", "E-posta", "Sonuç", "IP", "Kullanıcı Id", "Kullanıcı"
    };

    public sealed record Row(
        string AttemptedAt,
        string Email,
        string Result,
        string IpAddress,
        string UserId,
        string UserName);

    public static Row From(LoginAttempt a) => new(
        AppTime.ToLocal(a.AttemptedAt).ToString("dd.MM.yyyy HH:mm:ss"),
        a.Email,
        Ui.LoginResultText(a.Result),
        a.IpAddress ?? "",
        a.UserId?.ToString() ?? "",
        a.User?.FullName ?? "");

    public static string Build(IEnumerable<Row> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(TicketListCsv.Separator, Headers.Select(TicketListCsv.Escape)));
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(TicketListCsv.Separator, new[]
            {
                r.AttemptedAt, r.Email, r.Result, r.IpAddress, r.UserId, r.UserName
            }.Select(TicketListCsv.Escape)));
        }
        return sb.ToString();
    }
}
