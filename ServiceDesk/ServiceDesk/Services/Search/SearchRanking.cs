using System.Linq.Expressions;
using System.Reflection;
using ServiceDesk.Models;

namespace ServiceDesk.Services.Search;

// Arama skorunu bir LINQ İFADESİ olarak kurar; sorgu sunucuda sıralanır.
//
// Neden ifade ağacı: skoru bellekte hesaplamak için eşleşen bütün satırları
// çekmek gerekirdi ve sayfalama (Skip/Take) SQL tarafında yapılamazdı. Skor
// ifade olarak kurulduğunda EF bunu "CASE WHEN CHARINDEX(...) > 0 THEN 10
// ELSE 0 END + ..." toplamına çevirir; ORDER BY da veritabanında çalışır.
//
// Aynı ifade LINQ-to-Objects üzerinde de çalışır (List.AsQueryable()), bu
// sayede sıralama kuralı veritabanı olmadan birim testi edilebiliyor.
//
// NOT (full-text): SQL Server full-text index (CONTAINSTABLE / RANK) daha
// iyi sıralama üretirdi, ama bu kurulumda kullanılamıyor: veritabanı
// LocalDB ve SERVERPROPERTY('IsFullTextInstalled') = 0, yani full-text
// bileşeni yüklü değil; "CREATE FULLTEXT CATALOG" içeren bir migration
// geliştirme ortamında hata verirdi. Bu yüzden skor LIKE/CHARINDEX
// üzerinden kuruluyor — bilgi bankası ölçeğinde (yüzlerce makale) bu
// yeterli. Full-text kurulu bir sunucuya geçilirse değişmesi gereken tek
// yer burasıdır.
public static class SearchRanking
{
    // Aranacak bir alan: metni veren seçici + ağırlıkları.
    //
    // Seçici null dönebilir (Ticket.Number, kategorisiz makale): null
    // kontrolü üretilen ifadeye ekleniyor, böylece hem SQL'de hem bellekte
    // güvenli çalışır.
    public sealed record Field<T>(
        Expression<Func<T, string?>> Selector,
        int Weight,
        int PrefixBonus = 0,
        int PhraseWeight = 0);

    // Kurulan ifadeler. Score sıralama, Matches süzme içindir.
    public sealed class Ranked<T>
    {
        public required Expression<Func<T, int>> Score { get; init; }

        // Skoru sıfırdan büyük olan satırlar: en az bir kelime bir alanda geçti
        public required Expression<Func<T, bool>> Matches { get; init; }

        // Kaç ARAMA KELİMESİ eşleşti (alan sayısı değil). Chatbot "en az iki
        // kelime tutmalı" kuralını bunun üzerinden kuruyor.
        public required Expression<Func<T, int>> MatchedWordCount { get; init; }

        public required int WordCount { get; init; }

        public Expression<Func<T, bool>> MatchesAtLeast(int words)
        {
            var p = MatchedWordCount.Parameters[0];
            return Expression.Lambda<Func<T, bool>>(
                Expression.GreaterThanOrEqual(MatchedWordCount.Body, Expression.Constant(words)), p);
        }
    }

    private static readonly MethodInfo ContainsMethod =
        typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;

    private static readonly MethodInfo StartsWithMethod =
        typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!;

    private static readonly MethodInfo ToLowerMethod =
        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

    // ───── Hazır kümeler ─────────────────────────────────────────────────

    public static readonly Field<KnowledgeArticle>[] ArticleFields =
    {
        new(a => a.Title, SearchTerms.TitleWeight,
            PrefixBonus: SearchTerms.TitlePrefixBonus, PhraseWeight: SearchTerms.TitlePhraseWeight),
        new(a => a.Body, SearchTerms.BodyWeight, PhraseWeight: SearchTerms.BodyPhraseWeight),
        // Kategorisiz makalede null: ifadeye null kontrolü ekleniyor
        new(a => a.Category == null ? null : a.Category.Name, SearchTerms.CategoryWeight)
    };

    public static readonly Field<Ticket>[] TicketFields =
    {
        new(t => t.Title, SearchTerms.TicketTitleWeight,
            PrefixBonus: SearchTerms.TitlePrefixBonus, PhraseWeight: SearchTerms.TitlePhraseWeight),
        // Numarası henüz üretilmemiş eski talepte null olabilir
        new(t => t.Number, SearchTerms.TicketNumberWeight),
        new(t => t.RequesterName, SearchTerms.TicketRequesterWeight)
    };

    public static Ranked<KnowledgeArticle>? Articles(string? query) => Build(query, ArticleFields);

    public static Ranked<Ticket>? Tickets(string? query) => Build(query, TicketFields);

    // ───── İfade kurulumu ────────────────────────────────────────────────

    // Aranacak anlamlı kelime yoksa null döner: çağıran kendi varsayılan
    // sıralamasını (ör. en çok kullanılan makale) uygular.
    public static Ranked<T>? Build<T>(string? query, Field<T>[] fields, int minWordLength = SearchTerms.MinWordLength)
    {
        var words = SearchTerms.Words(query, minWordLength);
        if (words.Count == 0) return null;

        var p = Expression.Parameter(typeof(T), "x");

        Expression score = Expression.Constant(0);
        Expression matchedWords = Expression.Constant(0);

        foreach (var word in words)
        {
            Expression? anyField = null;

            foreach (var field in fields)
            {
                var text = Rebind(field.Selector, p);

                // (metin != null && metin.ToLower().Contains(kelime)) ? ağırlık : 0
                var contains = Contains(text, word);
                score = Expression.Add(score, Score(contains, field.Weight));

                if (field.PrefixBonus > 0)
                    score = Expression.Add(score, Score(StartsWith(text, word), field.PrefixBonus));

                anyField = anyField == null ? contains : Expression.OrElse(anyField, contains);
            }

            // Kelime HERHANGİ bir alanda geçtiyse eşleşen kelime sayısı artar
            if (anyField != null)
                matchedWords = Expression.Add(matchedWords, Score(anyField, 1));
        }

        // Öbek (bitişik ifade) puanı yalnızca çok kelimeli aramada anlamlı:
        // tek kelimede öbek = kelimenin kendisi, puan iki kez sayılırdı.
        var phrase = SearchTerms.Normalize(query);
        if (words.Count > 1)
        {
            foreach (var field in fields.Where(f => f.PhraseWeight > 0))
                score = Expression.Add(score,
                    Score(Contains(Rebind(field.Selector, p), phrase), field.PhraseWeight));
        }

        return new Ranked<T>
        {
            Score = Expression.Lambda<Func<T, int>>(score, p),
            Matches = Expression.Lambda<Func<T, bool>>(
                Expression.GreaterThan(score, Expression.Constant(0)), p),
            MatchedWordCount = Expression.Lambda<Func<T, int>>(matchedWords, p),
            WordCount = words.Count
        };
    }

    // koşul ? ağırlık : 0
    private static Expression Score(Expression test, int weight) =>
        Expression.Condition(test, Expression.Constant(weight), Expression.Constant(0));

    private static Expression Contains(Expression text, string value) =>
        NullSafe(text, lower => Expression.Call(lower, ContainsMethod, Expression.Constant(value)));

    private static Expression StartsWith(Expression text, string value) =>
        NullSafe(text, lower => Expression.Call(lower, StartsWithMethod, Expression.Constant(value)));

    // metin != null && <koşul(metin.ToLower())>
    //
    // Null kontrolü SQL'de gereksiz görünür (NULL LIKE '%x%' zaten false
    // döner) ama ifade bellekte de çalıştığı için şart: orada null üzerinde
    // ToLower() çağrısı NullReferenceException olurdu.
    private static Expression NullSafe(Expression text, Func<Expression, Expression> condition)
    {
        var notNull = Expression.NotEqual(text, Expression.Constant(null, typeof(string)));
        var lower = Expression.Call(text, ToLowerMethod);
        return Expression.AndAlso(notNull, condition(lower));
    }

    // Alan seçicisinin gövdesini, kurulmakta olan ifadenin parametresine bağlar:
    // (a => a.Title) + parametre x  →  x.Title
    private static Expression Rebind<T>(Expression<Func<T, string?>> selector, ParameterExpression parameter) =>
        new ParameterSwap(selector.Parameters[0], parameter).Visit(selector.Body)!;

    private sealed class ParameterSwap : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly Expression _to;

        public ParameterSwap(ParameterExpression from, Expression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _from ? _to : base.VisitParameter(node);
    }
}
