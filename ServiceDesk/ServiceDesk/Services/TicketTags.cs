using System.Globalization;

namespace ServiceDesk.Services;

// Talep etiketleri: virgülle ayrılmış, küçük harf, tekrarsız
public static class TicketTags
{
    public const int MaxTags = 10;
    public const int MaxTagLength = 30;
    private static readonly CultureInfo Tr = new("tr-TR");

    public static string Normalize(string tag) => tag.Trim().ToLower(Tr);

    // "VPN, laptop ,vpn" → "vpn,laptop". Hata varsa error dolu döner.
    public static (string? Value, string? Error) Parse(string? input)
    {
        var list = (input ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(t => t.Length > 0)
            .Distinct()
            .ToList();

        if (list.Count > MaxTags) return (null, $"En fazla {MaxTags} etiket girilebilir.");
        if (list.Any(t => t.Length > MaxTagLength)) return (null, $"Bir etiket en fazla {MaxTagLength} karakter olabilir.");

        var value = string.Join(",", list);
        if (value.Length > 300) return (null, "Etiketlerin toplam uzunluğu 300 karakteri geçemez.");
        return (value.Length == 0 ? null : value, null);
    }
}
