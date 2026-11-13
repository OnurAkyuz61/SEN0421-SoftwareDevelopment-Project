using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services.Search;

// ARAMA KATMANININ TEK GİRİŞ NOKTASI.
//
// Önce her ekran kendi aramasını yazıyordu: bilgi bankası
// "Title.Contains(q) || Body.Contains(q)" ile sıralamasız liste üretiyor,
// chatbot ayrı bir kelime sayma döngüsü çalıştırıyor, global arama ise
// yoktu. Sonuç: aynı sorguya üç farklı sıralama ve yetki kuralının her
// yerde elle tekrar edilmesi.
//
// Buradaki sözleşme:
//   • Sıralama SearchRanking'den gelir (tek skor kuralı).
//   • YETKİ sorgunun parçasıdır, çağıranın hatırlamasına bırakılmaz:
//     makaleler KnowledgePublishing.VisibleTo, talepler
//     TicketScope.SearchableBy süzgecinden geçer. Bu sınıftan IQueryable
//     dönen her yol kapsamı zaten uygulamış olur.
//   • Sayfalama / Take çağıranın işidir; sorgu açık döner ki liste ekranı
//     Paging.ToPageAsync ile SQL tarafında sayfalayabilsin.
public class SearchService
{
    // Öneri ucunun varsayılan ve en fazla sonuç sayısı
    public const int DefaultSuggestions = 5;
    public const int MaxSuggestions = 10;

    // Global aramada her grupta gösterilecek sonuç sayısı
    public const int DefaultGroupSize = 5;
    public const int MaxGroupSize = 20;

    // Arama kutusunun anlamlı sayılması için en az karakter
    public const int MinQueryLength = 2;

    private readonly AppDbContext _db;
    private readonly CurrentUser _current;

    public SearchService(AppDbContext db, CurrentUser current)
    {
        _db = db;
        _current = current;
    }

    private AppUser? Me => _current.User;

    public static bool IsSearchable(string? query) =>
        SearchTerms.Normalize(query).Length >= MinQueryLength;

    // ───── Bilgi bankası ─────────────────────────────────────────────────

    // Yetki kapsamlı ve SKORA GÖRE SIRALI makale sorgusu.
    //
    // Arama boşsa (liste ekranına filtresiz girildiğinde) skor kurulamaz;
    // o durumda eski davranış korunur: en çok kullanılan makale üstte.
    public IQueryable<KnowledgeArticle> Articles(string? query, int? categoryId = null)
    {
        var q = KnowledgePublishing.VisibleTo(
            _db.KnowledgeArticles.AsNoTracking().Include(a => a.Category), Me);

        // Kategori süzgeci skordan bağımsız: kullanıcı kategoriyi daralttıysa
        // sıralama o kategorinin içinde yapılır
        if (categoryId != null) q = q.Where(a => a.CategoryId == categoryId);

        var ranked = SearchRanking.Articles(query);
        if (ranked == null)
            return q.OrderByDescending(a => a.UsedCount).ThenBy(a => a.Title);

        return q.Where(ranked.Matches)
                .OrderByDescending(ranked.Score)
                // Eşit skorda daha çok işe yaramış makale önce gelir
                .ThenByDescending(a => a.UsedCount)
                .ThenBy(a => a.Title)
                .ThenBy(a => a.Id);
    }

    // Yalnızca YAYINDAKİ makaleler (chatbot, öneri ucu, talep çözüm listesi).
    // Taslak öneri olarak gösterilemez: henüz onaylanmamış metni kullanıcıya
    // çözüm diye sunmak olurdu.
    public IQueryable<KnowledgeArticle> PublishedArticles(string? query)
    {
        var q = _db.KnowledgeArticles.AsNoTracking()
            .Where(a => a.Status == KnowledgeArticleStatus.Published);

        var ranked = SearchRanking.Articles(query);
        if (ranked == null)
            return q.OrderByDescending(a => a.UsedCount).ThenBy(a => a.Title);

        return q.Where(ranked.Matches)
                .OrderByDescending(ranked.Score)
                .ThenByDescending(a => a.UsedCount)
                .ThenBy(a => a.Title)
                .ThenBy(a => a.Id);
    }

    public sealed record ArticleHit(int Id, string Title, string? CategoryName, int UsedCount,
                                    int HelpfulCount, int NotHelpfulCount);

    // Başlık yazılırken gösterilen ilk N makale önerisi
    public async Task<List<ArticleHit>> SuggestArticlesAsync(string? query, int take = DefaultSuggestions)
    {
        if (!IsSearchable(query)) return new List<ArticleHit>();

        return await PublishedArticles(query)
            .Take(Math.Clamp(take, 1, MaxSuggestions))
            .Select(a => new ArticleHit(a.Id, a.Title,
                a.Category == null ? null : a.Category.Name,
                a.UsedCount, a.HelpfulCount, a.NotHelpfulCount))
            .ToListAsync();
    }

    // Chatbot: sorunun kaç kelimesinin tutmasını beklediğimiz eşikle
    // sıralı makale listesi. Eşik chatbot'un kendi kararı (tek kelimelik
    // soruda 1, diğerlerinde 2), skor kuralı ise ortak.
    public async Task<List<ArticleHit>> AnswerArticlesAsync(string? question, int take)
    {
        var ranked = SearchRanking.Build(question, SearchRanking.ArticleFields, minWordLength: 3);
        if (ranked == null) return new List<ArticleHit>();

        var esik = ranked.WordCount >= 2 ? 2 : 1;

        return await _db.KnowledgeArticles.AsNoTracking()
            .Where(a => a.Status == KnowledgeArticleStatus.Published)
            .Where(ranked.MatchesAtLeast(esik))
            .OrderByDescending(ranked.Score)
            .ThenBy(a => a.Title)
            .Take(take)
            .Select(a => new ArticleHit(a.Id, a.Title,
                a.Category == null ? null : a.Category.Name,
                a.UsedCount, a.HelpfulCount, a.NotHelpfulCount))
            .ToListAsync();
    }

    // ───── Talepler ──────────────────────────────────────────────────────

    // Yetki kapsamlı, skora göre sıralı talep sorgusu
    public IQueryable<Ticket> Tickets(string? query)
    {
        var q = _db.Tickets.AsNoTracking()
            .Include(t => t.Category)
            .SearchableBy(Me!);

        var ranked = SearchRanking.Tickets(query);
        if (ranked == null)
            return q.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id);

        return q.Where(ranked.Matches)
                .OrderByDescending(ranked.Score)
                // Eşit skorda en yeni talep önce
                .ThenByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id);
    }

    // Aranan metin talep numarasıysa (INC-2026-00042 / #42 / 42) o talebi
    // döner. Yetki kapsamı burada da geçerli: başkasının talebinin numarasını
    // bilen son kullanıcı o talebe erişemez, sonuç "yok" olur.
    //
    // Numaranın ayırt edici parçası Id'dir; tam numara yazıldığında Number
    // kolonuyla da eşleşme aranır, çünkü numarası sonradan üretilmiş eski
    // kayıtlarda bu kolon boş olabilir.
    public async Task<Ticket?> FindByNumberAsync(string? query)
    {
        var reference = SearchTerms.ParseTicketReference(query);
        if (reference == null) return null;

        var id = reference.Id;
        var number = reference.Number;

        return await _db.Tickets.AsNoTracking()
            .Include(t => t.Category)
            .SearchableBy(Me!)
            .FirstOrDefaultAsync(t => t.Id == id || (number != null && t.Number == number));
    }

    // ───── Global arama ──────────────────────────────────────────────────

    public sealed record TicketHit(int Id, string Number, string Title,
                                   TicketType Type, TicketStatus Status, Priority Priority,
                                   string? CategoryName, string RequesterName, DateTime CreatedAt);

    public sealed record GlobalHits(
        string Query,
        TicketHit? NumberMatch,
        List<TicketHit> Tickets, int TicketTotal,
        List<ArticleHit> Articles, int ArticleTotal);

    // Talep numarası + talep başlığı + makale tek sorgu turunda.
    //
    // Numara eşleşmesi ayrı duruyor: "INC-2026-00042" yazan kişi o talebe
    // gitmek ister, başlık benzerliği listesini taramak istemez.
    public async Task<GlobalHits> GlobalAsync(string? query, int perGroup = DefaultGroupSize)
    {
        var normalized = SearchTerms.Normalize(query);
        var take = Math.Clamp(perGroup, 1, MaxGroupSize);

        if (!IsSearchable(query))
            return new GlobalHits(normalized, null, new(), 0, new(), 0);

        var numberMatch = await FindByNumberAsync(query);

        var ticketQuery = Tickets(query);
        // Numara eşleşmesi zaten ayrı gösteriliyor: listede tekrar etmesin
        if (numberMatch != null)
        {
            var haricId = numberMatch.Id;
            ticketQuery = ticketQuery.Where(t => t.Id != haricId);
        }

        var ticketTotal = await ticketQuery.CountAsync();
        var tickets = await ticketQuery.Take(take).Select(t => new TicketHit(
            t.Id, t.Number ?? ("#" + t.Id), t.Title, t.Type, t.Status, t.Priority,
            t.Category == null ? null : t.Category.Name, t.RequesterName, t.CreatedAt)).ToListAsync();

        // Makale tarafında yetki kuralı farklı: son kullanıcı yalnızca
        // yayındakini, destek personeli kendi taslağını da görür
        var articleQuery = Articles(query);
        var articleTotal = await articleQuery.CountAsync();
        var articles = await articleQuery.Take(take).Select(a => new ArticleHit(
            a.Id, a.Title, a.Category == null ? null : a.Category.Name,
            a.UsedCount, a.HelpfulCount, a.NotHelpfulCount)).ToListAsync();

        return new GlobalHits(normalized,
            numberMatch == null ? null : ToHit(numberMatch),
            tickets, ticketTotal, articles, articleTotal);
    }

    private static TicketHit ToHit(Ticket t) => new(
        t.Id, Ui.TicketNo(t), t.Title, t.Type, t.Status, t.Priority,
        t.Category?.Name, t.RequesterName, t.CreatedAt);
}
