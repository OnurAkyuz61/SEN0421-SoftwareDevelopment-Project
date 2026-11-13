using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Bir makalenin gözden geçirilmesi gerektiğini gösteren işaretler.
// [Flags]: bir makale aynı anda hem hiç kullanılmamış hem olumsuz oy almış
// olabilir; rapor bunların hepsini yan yana göstermeli.
[Flags]
public enum KnowledgeUsageIssue
{
    None = 0,

    // Yayımlanalı epey zaman geçmiş ama hiçbir talebi çözmemiş
    NeverUsed = 1,

    // Kullanılmış ama son kullanımı eskide kalmış
    Stale = 2,

    // Okuyucular "faydalı değil" demiş
    NegativeFeedback = 4
}

// Kullanılmayan / işe yaramayan makale ölçütleri.
//
// Eşikler raporun sorgu dizesinden gelir (varsayılanlar aşağıda): "eski" ve
// "oranı yüksek" kurumun kararıdır, kodun değil. ISO 9001 md. 9.1.3 analizin
// yapılmasını ister, eşiğin sabit olmasını değil.
//
// Saf sınıf: veritabanına ve HTTP'ye bağımlı değil, bu yüzden birim testi
// yazılabiliyor (bkz. ServiceDesk.Tests).
public static class KnowledgeUsagePolicy
{
    // Kullanım sayılmaması / son kullanımın eski sayılması için geçmesi
    // gereken gün. 90 gün: bir çeyrek boyunca hiçbir talebi çözmeyen makale
    // ya aranmıyor ya da güncelliğini yitirmiş.
    public const int DefaultStaleDays = 90;

    // Oran hesabının anlamlı olması için gereken en az oy sayısı. Tek bir
    // "faydalı değil" oyu makaleyi kötü yapmaz; eşik olmadan bir kişinin
    // oyu %100 olumsuz görünürdü.
    public const int DefaultMinVotes = 3;

    // Bu yüzdeden fazlası olumsuzsa makale işaretlenir
    public const int DefaultNotHelpfulPercent = 50;

    public static int TotalVotes(int helpfulCount, int notHelpfulCount) =>
        helpfulCount + notHelpfulCount;

    // "Faydalı değil" yüzdesi. Hiç oy yoksa null: "oy yok" ile "%0 olumsuz"
    // aynı şey değildir ve raporda ayrı gösterilmeli.
    public static int? NotHelpfulPercent(int helpfulCount, int notHelpfulCount)
    {
        var total = TotalVotes(helpfulCount, notHelpfulCount);
        if (total == 0) return null;

        // Tam sayıya yuvarlanır: eşik de yüzde olarak veriliyor
        return (int)Math.Round(notHelpfulCount * 100.0 / total, MidpointRounding.AwayFromZero);
    }

    public static bool HasNegativeFeedback(int helpfulCount, int notHelpfulCount,
                                           int minVotes = DefaultMinVotes,
                                           int percentThreshold = DefaultNotHelpfulPercent) =>
        TotalVotes(helpfulCount, notHelpfulCount) >= Math.Max(1, minVotes)
        && NotHelpfulPercent(helpfulCount, notHelpfulCount) >= percentThreshold;

    // Hiç kullanılmamış sayılması için makalenin bir süre ortalıkta durmuş
    // olması gerekir: dün yazılmış makale "kullanılmıyor" diye işaretlenirse
    // rapor her yeni makaleyle kirlenir.
    public static bool IsNeverUsed(int usedCount, DateTime createdAt, DateTime nowUtc,
                                  int staleDays = DefaultStaleDays) =>
        usedCount == 0 && createdAt <= nowUtc.AddDays(-staleDays);

    // Son kullanımı eskide kalmış. LastUsedAt boş olan ama sayacı dolu
    // makale burada işaretlenmez: geçişten önce kullanıldığı için son
    // kullanım anı BİLİNMİYOR olabilir; bilinmeyeni "eski" saymak raporu
    // yanlış doldurur (geçiş, Tickets tablosundan doldurabildiği kadarını
    // doldurur).
    public static bool IsStale(int usedCount, DateTime? lastUsedAt, DateTime nowUtc,
                               int staleDays = DefaultStaleDays) =>
        usedCount > 0 && lastUsedAt != null && lastUsedAt <= nowUtc.AddDays(-staleDays);

    // Raporun ihtiyacı olan alanlar makalenin tamamı değil; sorgu bunları
    // küçük bir projeksiyonla okuyabilsin diye ayrı aşırı yükleme var.
    public static KnowledgeUsageIssue Classify(int usedCount, DateTime createdAt, DateTime? lastUsedAt,
                                               int helpfulCount, int notHelpfulCount, DateTime nowUtc,
                                               int staleDays = DefaultStaleDays,
                                               int minVotes = DefaultMinVotes,
                                               int percentThreshold = DefaultNotHelpfulPercent)
    {
        var issues = KnowledgeUsageIssue.None;

        if (IsNeverUsed(usedCount, createdAt, nowUtc, staleDays))
            issues |= KnowledgeUsageIssue.NeverUsed;

        if (IsStale(usedCount, lastUsedAt, nowUtc, staleDays))
            issues |= KnowledgeUsageIssue.Stale;

        if (HasNegativeFeedback(helpfulCount, notHelpfulCount, minVotes, percentThreshold))
            issues |= KnowledgeUsageIssue.NegativeFeedback;

        return issues;
    }

    public static KnowledgeUsageIssue Classify(KnowledgeArticle article, DateTime nowUtc,
                                               int staleDays = DefaultStaleDays,
                                               int minVotes = DefaultMinVotes,
                                               int percentThreshold = DefaultNotHelpfulPercent) =>
        Classify(article.UsedCount, article.CreatedAt, article.LastUsedAt,
                 article.HelpfulCount, article.NotHelpfulCount,
                 nowUtc, staleDays, minVotes, percentThreshold);

    // Raporda satırın gerekçesi: insan okuyacağı metinler
    public static IReadOnlyList<string> Describe(KnowledgeUsageIssue issues) =>
        new[]
        {
            (issues.HasFlag(KnowledgeUsageIssue.NeverUsed), "hiç kullanılmadı"),
            (issues.HasFlag(KnowledgeUsageIssue.Stale), "son kullanımı eski"),
            (issues.HasFlag(KnowledgeUsageIssue.NegativeFeedback), "olumsuz geri bildirim")
        }
        .Where(x => x.Item1).Select(x => x.Item2).ToList();

    // ───── Oy sayaçlarının değişimi ──────────────────────────────────────
    //
    // Oy verme / oy değiştirme sonrası özet sayaçlara uygulanacak fark.
    // Sayaçlar tek SQL UPDATE ile değiştiği için (oku-değiştir-yaz yok)
    // farkın kendisi burada hesaplanır ve birim testi yazılabilir.
    public readonly record struct VoteDelta(int Helpful, int NotHelpful)
    {
        public bool IsEmpty => Helpful == 0 && NotHelpful == 0;
    }

    // previousVote: kullanıcının daha önceki oyu (yoksa null)
    public static VoteDelta Delta(bool? previousVote, bool newVote)
    {
        // Aynı oy tekrar verildi: sayaç değişmez
        if (previousVote == newVote) return new VoteDelta(0, 0);

        return previousVote switch
        {
            null => newVote ? new VoteDelta(1, 0) : new VoteDelta(0, 1),
            true => new VoteDelta(-1, 1),    // faydalı → faydalı değil
            false => new VoteDelta(1, -1)     // faydalı değil → faydalı
        };
    }
}
