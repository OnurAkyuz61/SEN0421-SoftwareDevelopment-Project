using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Makale kullanım ölçümü: oy oranı, "hiç kullanılmadı / son kullanımı eski"
// eşikleri ve raporun satırı neden işaretlediği.
public class KnowledgeUsageTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);

    private const int StaleDays = KnowledgeUsagePolicy.DefaultStaleDays;   // 90
    private const int MinVotes = KnowledgeUsagePolicy.DefaultMinVotes;     // 3
    private const int Percent = KnowledgeUsagePolicy.DefaultNotHelpfulPercent; // 50

    private static KnowledgeArticle Makale(int usedCount = 0, double createdDaysAgo = 365,
                                           double? lastUsedDaysAgo = null,
                                           int helpful = 0, int notHelpful = 0) =>
        new()
        {
            Id = 1,
            Title = "Yazıcı kağıt sıkışması",
            Status = KnowledgeArticleStatus.Published,
            UsedCount = usedCount,
            CreatedAt = Now.AddDays(-createdDaysAgo),
            LastUsedAt = lastUsedDaysAgo == null ? null : Now.AddDays(-lastUsedDaysAgo.Value),
            HelpfulCount = helpful,
            NotHelpfulCount = notHelpful
        };

    // ───── Oy oranı ──────────────────────────────────────────────────────

    [Fact]
    public void Oy_yoksa_oran_hesaplanmaz()
    {
        // "Oy yok" ile "%0 olumsuz" aynı şey değil
        Assert.Null(KnowledgeUsagePolicy.NotHelpfulPercent(0, 0));
        Assert.Equal(0, KnowledgeUsagePolicy.TotalVotes(0, 0));
    }

    [Theory]
    [InlineData(0, 1, 100)]
    [InlineData(1, 1, 50)]
    [InlineData(3, 1, 25)]
    [InlineData(1, 3, 75)]
    [InlineData(2, 1, 33)]   // 33,33 → 33
    [InlineData(1, 2, 67)]   // 66,67 → 67
    public void Olumsuz_yuzdesi_yuvarlanir(int helpful, int notHelpful, int beklenen) =>
        Assert.Equal(beklenen, KnowledgeUsagePolicy.NotHelpfulPercent(helpful, notHelpful));

    [Fact]
    public void Tek_olumsuz_oy_makaleyi_isaretlemez()
    {
        // 1 oy %100 olumsuz görünür ama anlamlı değil: en az oy eşiği var
        Assert.False(KnowledgeUsagePolicy.HasNegativeFeedback(0, 1, MinVotes, Percent));
        Assert.False(KnowledgeUsagePolicy.HasNegativeFeedback(0, 2, MinVotes, Percent));
        Assert.True(KnowledgeUsagePolicy.HasNegativeFeedback(0, 3, MinVotes, Percent));
    }

    [Fact]
    public void Esigin_tam_sinirinda_isaretlenir()
    {
        // 2/4 = %50, eşik %50: sınır dahil
        Assert.True(KnowledgeUsagePolicy.HasNegativeFeedback(2, 2, MinVotes, Percent));
        // 1/4 = %25: eşiğin altında
        Assert.False(KnowledgeUsagePolicy.HasNegativeFeedback(3, 1, MinVotes, Percent));
    }

    [Fact]
    public void Olumlu_oy_cokken_makale_isaretlenmez() =>
        Assert.False(KnowledgeUsagePolicy.HasNegativeFeedback(20, 3, MinVotes, Percent));

    // ───── Kullanım eşikleri ─────────────────────────────────────────────

    [Fact]
    public void Yeni_makale_kullanilmadi_diye_isaretlenmez()
    {
        // Dün yazılmış makalenin henüz kullanılma şansı olmadı
        Assert.False(KnowledgeUsagePolicy.IsNeverUsed(0, Now.AddDays(-1), Now, StaleDays));
        Assert.True(KnowledgeUsagePolicy.IsNeverUsed(0, Now.AddDays(-StaleDays - 1), Now, StaleDays));
    }

    [Fact]
    public void Kullanilmis_makale_hic_kullanilmadi_sayilmaz() =>
        Assert.False(KnowledgeUsagePolicy.IsNeverUsed(1, Now.AddDays(-500), Now, StaleDays));

    [Fact]
    public void Son_kullanimi_eski_olan_isaretlenir()
    {
        Assert.True(KnowledgeUsagePolicy.IsStale(5, Now.AddDays(-StaleDays - 1), Now, StaleDays));
        Assert.False(KnowledgeUsagePolicy.IsStale(5, Now.AddDays(-10), Now, StaleDays));
    }

    // Geçişten önce kullanılmış makalelerde son kullanım anı bilinmiyor
    // olabilir; bilinmeyen "eski" sayılmaz
    [Fact]
    public void Son_kullanim_bilinmiyorsa_eski_sayilmaz() =>
        Assert.False(KnowledgeUsagePolicy.IsStale(5, null, Now, StaleDays));

    // Sınır dahil: tam 90 gün önce kullanılmış makale "eski" sayılır
    [Fact]
    public void Esik_tam_sinirinda_eski_sayilir()
    {
        Assert.True(KnowledgeUsagePolicy.IsStale(5, Now.AddDays(-StaleDays), Now, StaleDays));
        Assert.False(KnowledgeUsagePolicy.IsStale(5, Now.AddDays(-StaleDays).AddMinutes(1), Now, StaleDays));
    }

    // ───── Sınıflandırma ─────────────────────────────────────────────────

    [Fact]
    public void Saglikli_makale_isaretlenmez()
    {
        var m = Makale(usedCount: 12, lastUsedDaysAgo: 3, helpful: 10, notHelpful: 1);

        Assert.Equal(KnowledgeUsageIssue.None, KnowledgeUsagePolicy.Classify(m, Now));
        Assert.Empty(KnowledgeUsagePolicy.Describe(KnowledgeUsageIssue.None));
    }

    [Fact]
    public void Hic_kullanilmayan_eski_makale_isaretlenir()
    {
        var m = Makale(usedCount: 0, createdDaysAgo: 200);

        var sonuc = KnowledgeUsagePolicy.Classify(m, Now);

        Assert.True(sonuc.HasFlag(KnowledgeUsageIssue.NeverUsed));
        Assert.False(sonuc.HasFlag(KnowledgeUsageIssue.Stale));
        Assert.Contains("hiç kullanılmadı", KnowledgeUsagePolicy.Describe(sonuc));
    }

    // Bir makale aynı anda birden fazla gerekçeyle işaretlenebilir
    [Fact]
    public void Eski_ve_olumsuz_makale_iki_gerekceyle_isaretlenir()
    {
        var m = Makale(usedCount: 4, lastUsedDaysAgo: 200, helpful: 1, notHelpful: 5);

        var sonuc = KnowledgeUsagePolicy.Classify(m, Now);

        Assert.True(sonuc.HasFlag(KnowledgeUsageIssue.Stale));
        Assert.True(sonuc.HasFlag(KnowledgeUsageIssue.NegativeFeedback));
        Assert.False(sonuc.HasFlag(KnowledgeUsageIssue.NeverUsed));
        Assert.Equal(2, KnowledgeUsagePolicy.Describe(sonuc).Count);
    }

    [Fact]
    public void Esik_gevsetilirse_makale_listeden_cikar()
    {
        var m = Makale(usedCount: 4, lastUsedDaysAgo: 100, helpful: 1, notHelpful: 2);

        // Varsayılan eşiklerle işaretli
        Assert.NotEqual(KnowledgeUsageIssue.None, KnowledgeUsagePolicy.Classify(m, Now));

        // Eşikler gevşetilince temiz: kural kurumun kararına açık
        Assert.Equal(KnowledgeUsageIssue.None,
            KnowledgeUsagePolicy.Classify(m, Now, staleDays: 365, minVotes: 10, percentThreshold: 90));
    }

    [Fact]
    public void Projeksiyon_ve_varlik_asiri_yuklemeleri_ayni_sonucu_verir()
    {
        var m = Makale(usedCount: 0, createdDaysAgo: 400, helpful: 0, notHelpful: 4);

        Assert.Equal(
            KnowledgeUsagePolicy.Classify(m, Now),
            KnowledgeUsagePolicy.Classify(m.UsedCount, m.CreatedAt, m.LastUsedAt,
                m.HelpfulCount, m.NotHelpfulCount, Now));
    }

    [Fact]
    public void Gerekce_metinleri_sabit_sirada()
    {
        var hepsi = KnowledgeUsageIssue.NeverUsed | KnowledgeUsageIssue.Stale
                    | KnowledgeUsageIssue.NegativeFeedback;

        Assert.Equal(
            new[] { "hiç kullanılmadı", "son kullanımı eski", "olumsuz geri bildirim" },
            KnowledgeUsagePolicy.Describe(hepsi));
    }
}
