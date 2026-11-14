using System.Security.Cryptography;
using System.Text;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Kullanıcı CSV içe aktarma kuralları (saf; birim testi yazılabiliyor).
//
// Ayırıcı: noktalı virgül (Türkçe Excel ile uyumlu, rapor CSV'leriyle aynı).
// Başlık satırı zorunlu. En fazla MaxRows veri satırı.
//
// Sütunlar (sıra esnek, başlık adına göre):
//   Email*, FirstName*/Ad*, LastName*/Soyad*, Phone/Telefon,
//   Role/Rol, Department/Birim, Team/Ekip, Password/Şifre
public static class UserCsvImport
{
    public const int MaxRows = 500;
    public const char Separator = ';';

    public static readonly string[] TemplateHeaders =
    {
        "Email", "FirstName", "LastName", "Phone", "Role", "Department", "Team", "Password"
    };

    public record ParsedRow(
        int Line,
        string Email,
        string FirstName,
        string LastName,
        string? Phone,
        UserRole Role,
        string? DepartmentName,
        string? TeamName,
        string? Password);

    public record ParseResult(
        List<ParsedRow> Rows,
        List<(int Line, string Error)> Errors);

    // Ham CSV metnini satır satır çözer. Dosya boş / başlık yoksa Errors dolu gelir.
    public static ParseResult Parse(string? content)
    {
        var rows = new List<ParsedRow>();
        var errors = new List<(int Line, string Error)>();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add((0, "CSV dosyası boş."));
            return new ParseResult(rows, errors);
        }

        // UTF-8 BOM
        if (content.Length > 0 && content[0] == '\uFEFF')
            content = content[1..];

        var lines = SplitLines(content);
        if (lines.Count == 0)
        {
            errors.Add((0, "CSV dosyası boş."));
            return new ParseResult(rows, errors);
        }

        var headerCells = SplitCsvLine(lines[0]);
        var map = MapHeaders(headerCells);
        if (!map.ContainsKey("email") || !map.ContainsKey("firstname") || !map.ContainsKey("lastname"))
        {
            errors.Add((1, "Başlık satırında Email, FirstName (veya Ad) ve LastName (veya Soyad) zorunludur."));
            return new ParseResult(rows, errors);
        }

        var dataLines = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (dataLines.Count > MaxRows)
        {
            errors.Add((0, $"En fazla {MaxRows} satır aktarılabilir (dosyada {dataLines.Count} veri satırı var)."));
            return new ParseResult(rows, errors);
        }

        for (var i = 0; i < dataLines.Count; i++)
        {
            var lineNo = i + 2; // 1 = başlık
            var cells = SplitCsvLine(dataLines[i]);
            var email = Cell(cells, map, "email")?.Trim().ToLowerInvariant() ?? "";
            var first = CatalogRules.NormalizeName(Cell(cells, map, "firstname"));
            var last = CatalogRules.NormalizeName(Cell(cells, map, "lastname"));
            var phoneRaw = Cell(cells, map, "phone");
            var roleRaw = Cell(cells, map, "role");
            var dept = CatalogRules.NormalizeName(Cell(cells, map, "department"));
            var team = CatalogRules.NormalizeName(Cell(cells, map, "team"));
            var password = Cell(cells, map, "password");
            if (string.IsNullOrWhiteSpace(password)) password = null;
            else password = password.Trim();

            if (email.Length == 0)
            {
                errors.Add((lineNo, "E-posta boş."));
                continue;
            }
            if (!email.Contains('@') || email.Length > 150)
            {
                errors.Add((lineNo, "Geçersiz e-posta."));
                continue;
            }
            if (first.Length == 0 || first.Length > 50)
            {
                errors.Add((lineNo, "Ad boş veya çok uzun."));
                continue;
            }
            if (last.Length == 0 || last.Length > 50)
            {
                errors.Add((lineNo, "Soyad boş veya çok uzun."));
                continue;
            }

            var phone = PhoneRules.Normalize(phoneRaw);
            if (PhoneRules.Validate(phone) is { } phoneErr)
            {
                errors.Add((lineNo, phoneErr));
                continue;
            }

            if (!TryParseRole(roleRaw, out var role))
            {
                errors.Add((lineNo, "Rol geçersiz (Kullanıcı / Destek / Yönetici veya User / Agent / Admin)."));
                continue;
            }

            if (password != null && PasswordRules.Validate(password) is { } pwdErr)
            {
                errors.Add((lineNo, pwdErr));
                continue;
            }

            rows.Add(new ParsedRow(lineNo, email, first, last, phone, role,
                string.IsNullOrEmpty(dept) ? null : dept,
                string.IsNullOrEmpty(team) ? null : team,
                password));
        }

        // Dosya içi yinelenen e-posta
        foreach (var g in rows.GroupBy(r => r.Email).Where(g => g.Count() > 1))
        {
            foreach (var r in g.Skip(1))
                errors.Add((r.Line, $"Aynı e-posta dosyada birden fazla: {r.Email}"));
            rows.RemoveAll(r => r.Email == g.Key && r.Line != g.First().Line);
        }

        return new ParseResult(rows, errors.OrderBy(e => e.Line).ToList());
    }

    public static bool TryParseRole(string? raw, out UserRole role)
    {
        role = UserRole.User;
        if (string.IsNullOrWhiteSpace(raw)) return true; // varsayılan Kullanıcı

        var t = raw.Trim();
        if (Enum.TryParse<UserRole>(t, ignoreCase: true, out role)) return true;

        if (CatalogRules.SameName(t, "Kullanıcı") || CatalogRules.SameName(t, "User"))
        { role = UserRole.User; return true; }
        if (CatalogRules.SameName(t, "Destek") || CatalogRules.SameName(t, "Agent"))
        { role = UserRole.Agent; return true; }
        if (CatalogRules.SameName(t, "Yönetici") || CatalogRules.SameName(t, "Admin"))
        { role = UserRole.Admin; return true; }

        return false;
    }

    // PasswordRules'u karşılayan geçici şifre (yönetici sonuç ekranında bir kez görür)
    public static string GenerateTempPassword()
    {
        var n = RandomNumberGenerator.GetInt32(100000, 1000000);
        return $"Gecici#{n}Aa";
    }

    public static string TemplateCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(Separator, TemplateHeaders));
        sb.AppendLine(string.Join(Separator, new[]
        {
            "ornek@servicedesk.local", "Ayşe", "Yılmaz", "05321234567",
            "Kullanıcı", "Bilgi İşlem", "", ""
        }.Select(Escape)));
        return sb.ToString();
    }

    public static string Escape(string v)
    {
        if (v.IndexOfAny(new[] { Separator, '"', '\n', '\r' }) >= 0)
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }

    // ── CSV satır ayırıcı (tırnaklı alanlar) ─────────────────────────────

    private static List<string> SplitLines(string content)
    {
        var list = new List<string>();
        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } line)
            list.Add(line);
        return list;
    }

    public static List<string> SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == Separator)
            {
                cells.Add(sb.ToString());
                sb.Clear();
            }
            else sb.Append(c);
        }
        cells.Add(sb.ToString());
        return cells;
    }

    private static Dictionary<string, int> MapHeaders(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var key = NormalizeHeader(headers[i]);
            if (key != null && !map.ContainsKey(key))
                map[key] = i;
        }
        return map;
    }

    private static string? NormalizeHeader(string? h)
    {
        if (string.IsNullOrWhiteSpace(h)) return null;
        var t = h.Trim().ToLowerInvariant();
        return t switch
        {
            "email" or "e-posta" or "eposta" => "email",
            "firstname" or "first_name" or "ad" or "adi" => "firstname",
            "lastname" or "last_name" or "soyad" or "soyadi" => "lastname",
            "phone" or "telefon" or "tel" => "phone",
            "role" or "rol" => "role",
            "department" or "birim" => "department",
            "team" or "ekip" => "team",
            "password" or "sifre" or "şifre" => "password",
            _ => null
        };
    }

    private static string? Cell(List<string> cells, Dictionary<string, int> map, string key) =>
        map.TryGetValue(key, out var i) && i < cells.Count ? cells[i] : null;
}
