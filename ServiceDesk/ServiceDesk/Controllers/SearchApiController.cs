using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Search;

namespace ServiceDesk.Controllers;

// ARAMA JSON UÇLARI.
//
//   Makale önerisi  GET  api/search/articles?q=yazıcı&take=5
//   Global arama    GET  api/search?q=INC-2026-00042&perGroup=5
//
// Sözleşme Models/ApiContracts.cs'teki desenin aynısı: camelCase alanlar,
// liste gövdesi { items: [...] }, hata gövdesi { error: "..." }, enum'lar
// sayısal değerleriyle (Türkçe karşılıkları da gönderilir ki istemci
// api/enums sözlüğüne gitmeden gösterebilsin), tarihler UTC.
//
// YETKİ: [ApiAuthorizeRole] — oturum yoksa 401, gövdesi { error }. Sonuçların
// kapsamı SearchService'in içinde: talepler TicketScope.SearchableBy (son
// kullanıcı yalnızca kendi talebi), makaleler KnowledgePublishing.VisibleTo
// (son kullanıcı yalnızca yayındakiler). Bu controller kapsamı yeniden
// yazmaz, yoksa kural iki yerde durur.
//
// İSTEK SINIRI: RateLimitPolicies.Search — talep/arşiv/bilgi bankası
// aramalarıyla aynı politika. Yazarken her tuşta istek atan öneri ucu bu
// sınırın asıl muhatabı; aşıldığında 429 + { error } döner (RateLimiting).
[ApiController]
[Route("api/search")]
[ApiAuthorizeRole]
[EnableRateLimiting(RateLimitPolicies.Search)]
public class SearchApiController : ControllerBase
{
    private readonly SearchService _search;
    private readonly CurrentUser _current;

    public SearchApiController(SearchService search, CurrentUser current)
    {
        _search = search;
        _current = current;
    }

    // Öneri satırı: kullanıcıya gösterilecek en az bilgi. Gövde metni
    // gönderilmez (öneri listesinde yeri yok, boşuna ağırlık).
    public record ArticleSuggestionDto(
        int Id, string Title, string? CategoryName, string Url,
        int UsedCount, int HelpfulCount, int NotHelpfulCount);

    public record TicketHitDto(
        int Id, string Number, string Title, string Url,
        TicketType Type, string TypeText,
        TicketStatus Status, string StatusText, string StatusTag,
        Priority Priority, string PriorityText, string PriorityTag,
        string? CategoryName, string RequesterName, DateTime CreatedAt);

    public record GlobalSearchDto(
        string Query,
        TicketHitDto? NumberMatch,
        ApiSearchGroup<TicketHitDto> Tickets,
        ApiSearchGroup<ArticleSuggestionDto> Articles,
        int TotalCount);

    // ───── Makale önerisi ────────────────────────────────────────────────
    //
    // Talep başlığı yazılırken çağrılır: kullanıcı talebi göndermeden önce
    // çözümü kendi bulabilsin (bilgi bankasının asıl amacı). Yalnızca
    // YAYINDAKİ makaleler önerilir.
    [HttpGet("articles")]
    public async Task<IActionResult> Articles(string? q, int take = SearchService.DefaultSuggestions)
    {
        // Çok kısa metin için öneri üretilmez: iki harf neredeyse her
        // makaleyle eşleşir, liste gürültüden ibaret olurdu. Hata değil,
        // boş liste: istemci her tuşta çağırıyor.
        var hits = await _search.SuggestArticlesAsync(q, take);

        return Ok(ApiItems<ArticleSuggestionDto>.From(
            hits.Select(ToDto).ToList()));
    }

    // ───── Global arama ──────────────────────────────────────────────────
    //
    // Talep numarası, talep başlığı ve makale tek uçta; sonuçlar türe göre
    // gruplu döner. Numara eşleşmesi gruplardan ayrı bir alanda: tam numara
    // yazan kişi listeyi taramak değil o talebe gitmek ister.
    [HttpGet]
    public async Task<IActionResult> Global(string? q, int perGroup = SearchService.DefaultGroupSize)
    {
        if (!SearchService.IsSearchable(q))
            return BadRequest(new ApiError($"Arama en az {SearchService.MinQueryLength} karakter olmalı."));

        var hits = await _search.GlobalAsync(q, perGroup);

        var tickets = new ApiSearchGroup<TicketHitDto>(
            "ticket", "Talepler", hits.TicketTotal,
            hits.Tickets.Select(ToDto).ToList());

        var articles = new ApiSearchGroup<ArticleSuggestionDto>(
            "article", "Bilgi bankası", hits.ArticleTotal,
            hits.Articles.Select(ToDto).ToList());

        return Ok(new GlobalSearchDto(
            hits.Query,
            hits.NumberMatch == null ? null : ToDto(hits.NumberMatch),
            tickets,
            articles,
            // Numara eşleşmesi de bir sonuçtur: "sonuç yok" kararını
            // istemci tek alana bakarak verebilsin
            hits.TicketTotal + hits.ArticleTotal + (hits.NumberMatch == null ? 0 : 1)));
    }

    private ArticleSuggestionDto ToDto(SearchService.ArticleHit a) => new(
        a.Id, a.Title, a.CategoryName,
        Url.Action("Details", "Knowledge", new { id = a.Id }) ?? "",
        a.UsedCount, a.HelpfulCount, a.NotHelpfulCount);

    private TicketHitDto ToDto(SearchService.TicketHit t) => new(
        t.Id, t.Number, t.Title,
        // Son kullanıcı portal detayını, destek personeli yönetim detayını
        // açar: /Tickets son kullanıcıya kapalı (AuthorizeRole), oraya
        // yönlendiren bir bağlantı çalışmayan bir sonuç olurdu
        (_current.IsStaff
            ? Url.Action("Details", "Tickets", new { id = t.Id })
            : Url.Action("Details", "Portal", new { id = t.Id })) ?? "",
        t.Type, Ui.TypeText(t.Type),
        t.Status, Ui.StatusText(t.Status), Ui.StatusTag(t.Status),
        t.Priority, Ui.PriorityText(t.Priority), Ui.PriorityTag(t.Priority),
        t.CategoryName, t.RequesterName, t.CreatedAt);
}
