using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Yönetim gözden geçirme paketinin (md. 9.3) hesapları: dönem penceresi,
// KPI snapshot toplama, dönem karşılaştırması, zayıf anket boyutları ve CSV.
public class ManagementReviewTests
{
    // ───── Dönem ─────────────────────────────────────────────────────────

    [Fact]
    public void Bitis_gunu_donemin_icinde_sayilir()
    {
        var p = new ManagementReviewPeriod(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30));

        Assert.Equal(30, p.Days);
        Assert.Equal(AppTime.ToUtc(new DateTime(2026, 11, 1)), p.FromUtc);
        // Üst sınır dışlayıcı: 30 Kasım'ın tamamı kapsanır
        Assert.Equal(AppTime.ToUtc(new DateTime(2026, 12, 1)), p.ToUtc);
    }

    [Fact]
    public void Karsilastirma_donemi_hemen_onceki_esit_uzunluktaki_penceredir()
    {
        var p = new ManagementReviewPeriod(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30));
        var onceki = p.Previous();

        Assert.Equal(new DateTime(2026, 10, 2), onceki.FromLocal);
        Assert.Equal(new DateTime(2026, 10, 31), onceki.ToLocal);
        Assert.Equal(p.Days, onceki.Days);

        // Pencereler bitişik ama örtüşmüyor
        Assert.Equal(p.FromUtc, onceki.ToUtc);
    }

    [Fact]
    public void Tek_gunluk_donem_de_gecerlidir()
    {
        var p = new ManagementReviewPeriod(new DateTime(2026, 11, 14), new DateTime(2026, 11, 14));

        Assert.Equal(1, p.Days);
        Assert.Equal(new DateTime(2026, 11, 13), p.Previous().FromLocal);
    }

    // ───── KPI toplama ───────────────────────────────────────────────────

    private static KpiSnapshot Snap(int day, int total, int resolved, int breached,
                                    decimal avgResolution, decimal csat, int responses,
                                    decimal frRate = 100m, int frBreached = 0,
                                    decimal avgFirstResponse = 0m, int reopen = 0, int low = 0) =>
        new()
        {
            Period = KpiPeriod.Daily,
            PeriodStart = AppTime.ToUtc(new DateTime(2026, 11, day)),
            PeriodEnd = AppTime.ToUtc(new DateTime(2026, 11, day).AddDays(1)),
            TotalTickets = total,
            ResolvedTickets = resolved,
            BreachedTickets = breached,
            ReopenCount = reopen,
            FirstResponseBreachedTickets = frBreached,
            FirstResponseComplianceRate = frRate,
            AvgResolutionHours = avgResolution,
            AvgFirstResponseHours = avgFirstResponse,
            CsatAverage = csat,
            SurveyResponseCount = responses,
            LowCsatCount = low
        };

    [Fact]
    public void Snapshot_yoksa_kpi_bos_doner()
    {
        var k = ManagementReviewPolicy.AggregateKpi(new List<KpiSnapshot>());

        Assert.False(k.HasData);
        Assert.Equal(0, k.TotalTickets);
        // Ölçüm yoksa uyum %100 kabul edilir (KpiService ile aynı davranış)
        Assert.Equal(100m, k.SlaComplianceRate);
    }

    [Fact]
    public void Sayilabilen_gostergeler_toplanir_sla_uyumu_yeniden_hesaplanir()
    {
        var snapshots = new List<KpiSnapshot>
        {
            Snap(1, total: 10, resolved: 8, breached: 1, avgResolution: 4m, csat: 4m, responses: 4),
            Snap(2, total: 30, resolved: 20, breached: 5, avgResolution: 10m, csat: 3m, responses: 6)
        };

        var k = ManagementReviewPolicy.AggregateKpi(snapshots);

        Assert.Equal(2, k.SnapshotDays);
        Assert.Equal(40, k.TotalTickets);
        Assert.Equal(28, k.ResolvedTickets);
        Assert.Equal(6, k.BreachedTickets);
        // Yüzdelerin düz ortalaması DEĞİL (o 86,7 verirdi): 100*(40-6)/40
        Assert.Equal(85.0m, k.SlaComplianceRate);
    }

    [Fact]
    public void Ortalama_cozum_suresi_cozulen_talep_sayisiyla_agirliklanir()
    {
        var snapshots = new List<KpiSnapshot>
        {
            Snap(1, total: 10, resolved: 8, breached: 0, avgResolution: 4m, csat: 0m, responses: 0),
            Snap(2, total: 30, resolved: 20, breached: 0, avgResolution: 10m, csat: 0m, responses: 0)
        };

        var k = ManagementReviewPolicy.AggregateKpi(snapshots);

        // (4*8 + 10*20) / 28 = 8,3 — düz ortalama 7,0 olurdu
        Assert.Equal(8.3m, k.AvgResolutionHours);
    }

    // Çözülen talebi olmayan gün ortalamayı aşağı çekmemeli: o günün
    // "0 saat"i ölçüm değil, ölçüm yokluğudur
    [Fact]
    public void Agirligi_sifir_olan_gun_ortalamaya_girmez()
    {
        var snapshots = new List<KpiSnapshot>
        {
            Snap(1, total: 5, resolved: 0, breached: 0, avgResolution: 0m, csat: 0m, responses: 0),
            Snap(2, total: 10, resolved: 10, breached: 0, avgResolution: 6m, csat: 0m, responses: 0)
        };

        var k = ManagementReviewPolicy.AggregateKpi(snapshots);

        Assert.Equal(6.0m, k.AvgResolutionHours);
    }

    [Fact]
    public void Csat_yanit_sayisina_gore_agirliklanir_ve_yanit_orani_yeniden_hesaplanir()
    {
        var snapshots = new List<KpiSnapshot>
        {
            Snap(1, total: 10, resolved: 10, breached: 0, avgResolution: 1m, csat: 5m, responses: 1),
            Snap(2, total: 10, resolved: 10, breached: 0, avgResolution: 1m, csat: 3m, responses: 9)
        };

        var k = ManagementReviewPolicy.AggregateKpi(snapshots);

        // (5*1 + 3*9) / 10 = 3,2 — düz ortalama 4,0 olurdu
        Assert.Equal(3.20m, k.CsatAverage);
        Assert.Equal(10, k.SurveyResponseCount);
        // 10 yanıt / 20 çözülen talep
        Assert.Equal(50.0m, k.CsatResponseRate);
    }

    // Değerlendirildikten sonra yeniden açılan talep yanıtı korur ama
    // ResolvedAt'i boşalır: oran %100'ü geçemez
    [Fact]
    public void Yanit_orani_yuzde_yuzu_gecemez()
    {
        var snapshots = new List<KpiSnapshot>
        {
            Snap(1, total: 10, resolved: 2, breached: 0, avgResolution: 1m, csat: 4m, responses: 8)
        };

        Assert.Equal(100m, ManagementReviewPolicy.AggregateKpi(snapshots).CsatResponseRate);
    }

    // ───── Dönem karşılaştırması ─────────────────────────────────────────

    [Fact]
    public void Gostergenin_yonu_anlamina_gore_yorumlanir()
    {
        // SLA uyumunun artması İYİ
        var uyum = new ManagementReviewDelta(92m, 85m, HigherIsBetter: true);
        Assert.True(uyum.Improved);
        Assert.False(uyum.Worsened);
        Assert.Equal(7m, uyum.Change);

        // Ortalama çözüm süresinin artması KÖTÜ
        var sure = new ManagementReviewDelta(12m, 8m, HigherIsBetter: false);
        Assert.True(sure.Worsened);
        Assert.False(sure.Improved);
    }

    [Fact]
    public void Degisim_yoksa_ne_iyilesme_ne_kotulesme_vardir()
    {
        var d = new ManagementReviewDelta(50m, 50m, HigherIsBetter: true);

        Assert.False(d.Improved);
        Assert.False(d.Worsened);
        Assert.Equal(0m, d.ChangePercent);
    }

    [Fact]
    public void Onceki_donem_sifirsa_yuzde_degisim_tanimsizdir()
    {
        var d = new ManagementReviewDelta(5m, 0m, HigherIsBetter: true);

        Assert.Null(d.ChangePercent);      // "%∞ artış" yazılmaz
        Assert.Equal(5m, d.Change);
        Assert.True(d.Improved);
    }

    // ───── Anket boyutları ───────────────────────────────────────────────

    [Fact]
    public void Zayif_boyutlar_esigin_altindakiler_en_dusuk_basta()
    {
        var dims = new List<SurveyDimension>
        {
            new(1, "Çözüm hızı", 10, 4.4),
            new(2, "Kalıcı çözüm", 10, 2.1),
            new(3, "İlgi", 10, 2.4),
            new(4, "Hiç cevaplanmadı", 0, 0)
        };

        var zayif = ManagementReviewPolicy.WeakDimensions(dims, threshold: 2.5);

        Assert.Equal(new[] { "Kalıcı çözüm", "İlgi" }, zayif.Select(d => d.Question));
        // Cevabı olmayan soru zayıf sayılmaz: ölçüm yok
        Assert.DoesNotContain("Hiç cevaplanmadı", zayif.Select(d => d.Question));
    }

    // ───── Uygunsuzluk: etkinlik doğrulama oranı ─────────────────────────

    [Fact]
    public void Etkinlik_dogrulama_orani_kapanan_kayitlar_uzerinden_cikar()
    {
        var now = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
        var rows = new List<NonconformityReportRow>
        {
            Kayit(1, NonconformityStatus.Closed, verified: true, now),
            Kayit(2, NonconformityStatus.Closed, verified: false, now),
            // Açık kayıt oranın paydasına girmez
            Kayit(3, NonconformityStatus.Open, verified: false, now)
        };

        var stat = NonconformityReportPolicy.Summarize(rows, now)
            .First(s => s.Source == NonconformitySource.Manual);

        Assert.Equal(2, stat.Closed);
        Assert.Equal(50.0, stat.VerificationRate!.Value, precision: 3);
    }

    [Fact]
    public void Kapanmamis_kaynakta_dogrulama_orani_tanimsizdir()
    {
        var now = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
        var rows = new List<NonconformityReportRow> { Kayit(1, NonconformityStatus.Open, false, now) };

        var stat = NonconformityReportPolicy.Summarize(rows, now)
            .First(s => s.Source == NonconformitySource.Manual);

        Assert.Null(stat.VerificationRate);   // "%0 doğrulanmış" diye gösterilmez
    }

    private static NonconformityReportRow Kayit(int id, NonconformityStatus status, bool verified, DateTime now) =>
        new(id, NonconformitySource.Manual, status, now.AddDays(-10), now.AddDays(-3),
            status == NonconformityStatus.Closed ? now.AddDays(-5) : null,
            verified, 1, "Ayşe Yılmaz", null, null, "Örnek");

    // ───── CSV ───────────────────────────────────────────────────────────

    [Fact]
    public void Csv_butun_bolumleri_uzun_bicimde_yazar()
    {
        var rapor = Rapor();

        var rows = ManagementReviewPolicy.CsvRows(rapor);
        var bolumler = rows.Select(r => r.Section).Distinct().ToList();

        // Paketin istenen altı bölümü (+ dönem başlığı) CSV'de de var
        Assert.Contains("Dönem", bolumler);
        Assert.Contains("KPI", bolumler);
        Assert.Contains("Uygunsuzluk", bolumler);
        Assert.Contains("Anket", bolumler);
        Assert.Contains("Bilgi bankası", bolumler);
        Assert.Contains("Depolama", bolumler);
        Assert.Contains("Eskalasyon", bolumler);
        Assert.Contains("Onay", bolumler);

        // KPI satırları önceki dönemi ve değişimi taşır
        var sla = rows.First(r => r.Section == "KPI" && r.Metric.StartsWith("SLA uyumu"));
        Assert.Equal("90.0", sla.Value);
        Assert.Equal("80.0", sla.Previous);
        Assert.Equal("10.0", sla.Change);

        // Sayılar kültürden bağımsız (nokta ondalık): Excel'de ; ayırıcıyla okunur
        Assert.All(rows, r => Assert.DoesNotContain(",", r.Value));
    }

    [Fact]
    public void Csv_hucrelerinde_satir_sonu_olmaz()
    {
        // Rapor metni kullanıcı verisinden geliyor (makale başlığı, chatbot
        // sorusu): CSV'nin satır yapısını bozmamalı — kaçış controller'da,
        // burada üretilen değerlerin kendisi tek satır olmalı
        var rows = ManagementReviewPolicy.CsvRows(Rapor());

        Assert.All(rows, r =>
        {
            Assert.DoesNotContain("\n", r.Metric);
            Assert.DoesNotContain("\n", r.Value);
        });
    }

    private static ManagementReviewReport Rapor()
    {
        var now = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
        var period = new ManagementReviewPeriod(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30));

        var kpi = ManagementReviewPolicy.AggregateKpi(new List<KpiSnapshot>
        {
            Snap(1, total: 10, resolved: 10, breached: 1, avgResolution: 5m, csat: 4m, responses: 5)
        });
        var onceki = ManagementReviewPolicy.AggregateKpi(new List<KpiSnapshot>
        {
            Snap(2, total: 10, resolved: 10, breached: 2, avgResolution: 6m, csat: 3m, responses: 4)
        });

        return new ManagementReviewReport(
            period, period.Previous(), kpi, onceki,
            NonconformityReportPolicy.Summarize(new List<NonconformityReportRow>(), now),
            new SurveySection(0, 0, 0, 0, 0, 0, 0m, new List<SurveyDimension>(), new List<CsatTrendPoint>()),
            new KnowledgeSection(0, 0, 0, 0, 0, 0, 0, 0, 0,
                new List<ArticleLine>(), new List<ArticleLine>(), 0, 0, new List<ChatbotQuestion>()),
            new StorageSection(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                true, 90, true, false, 0, 0),
            new OperationsSection(0, 0, 0, 0, 0, 0, new List<(int, int)>(),
                0, 0, 0, 0, 0, 0, 0, null),
            now);
    }

    // SLA uyumu 90 / 80 çıkacak şekilde: 1 ihlal / 10 ve 2 ihlal / 10
    [Fact]
    public void Ornek_rapor_beklenen_kpi_degerlerini_uretir()
    {
        var r = Rapor();

        Assert.Equal(90.0m, r.Kpi.SlaComplianceRate);
        Assert.Equal(80.0m, r.PreviousKpi.SlaComplianceRate);
        Assert.True(new ManagementReviewDelta(r.Kpi.SlaComplianceRate,
            r.PreviousKpi.SlaComplianceRate, true).Improved);
    }
}
