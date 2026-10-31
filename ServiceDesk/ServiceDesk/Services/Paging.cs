using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace ServiceDesk.Services;

// Liste ekranları için sayfalama. Filtreli sorgunun tamamı belleğe çekilmez;
// yalnızca istenen sayfa SQL'de Skip/Take ile alınır.
public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static async Task<(List<T> Items, PagerInfo Pager)> ToPageAsync<T>(
        this IQueryable<T> query, int page, int pageSize = DefaultPageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, new PagerInfo(page, pageSize, total, totalPages));
    }

    // Mevcut filtreleri koruyarak yalnızca page parametresini değiştiren URL
    public static string Url(HttpRequest request, int page)
    {
        var dict = request.Query
            .Where(kv => !string.Equals(kv.Key, "page", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => (string?)kv.Value.ToString());
        if (page > 1) dict["page"] = page.ToString();
        else dict.Remove("page");
        return QueryHelpers.AddQueryString(request.Path, dict);
    }
}

public sealed record PagerInfo(int Page, int PageSize, int TotalCount, int TotalPages)
{
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;
    public int FromItem => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int ToItem => Math.Min(Page * PageSize, TotalCount);
}
