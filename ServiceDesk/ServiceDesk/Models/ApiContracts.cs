using ServiceDesk.Services;

namespace ServiceDesk.Models;

// JSON uçlarının (api/*) ortak sözleşmesi. Tek yerde durmasının sebebi:
// sonradan eklenen her uç aynı kabuğu kullansın, istemci tarafı tek bir
// okuyucuyla yetinsin.
//
//   Alan adları : camelCase. ASP.NET Core'un System.Text.Json varsayılanı
//                 budur; Program.cs'te ayrıca bir ayar gerekmez.
//   Sayfalı liste: { items, page, pageSize, totalCount, totalPages,
//                    hasPrev, hasNext }
//   Hata         : { error: "Türkçe açıklama" }  — her hata durumu bu gövdeyi
//                   kullanır: doğrulama (400), çakışma (409), yetki (401/403,
//                   ApiAuthorizeRole) ve istek sınırı (429, RateLimiting).
//                   Bulunamayan kayıt gövdesiz 404 döner.
//   Enum'lar     : sayısal değerleriyle gider. Ekranda görünecek Türkçe
//                  karşılıkları ve etiket renkleri api/enums sözlüğünden alınır.
//   Tarihler     : UTC. Yerel saate çevirmek istemcinin / görünümün işi.
public sealed record ApiPage<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPrev,
    bool HasNext)
{
    // Sayfalamanın kendisi Paging.ToPageAsync'te (SQL tarafında Skip/Take);
    // buradaki tek iş PagerInfo'yu dışa açılan alan adlarına çevirmek.
    // PagerInfo'nun FromItem / ToItem alanları ekran içindir, sözleşmeye girmez.
    public static ApiPage<T> From(IReadOnlyList<T> items, PagerInfo pager) =>
        new(items, pager.Page, pager.PageSize, pager.TotalCount, pager.TotalPages,
            pager.HasPrev, pager.HasNext);
}

// Hata gövdesi: { "error": "..." }
public sealed record ApiError(string Error);
