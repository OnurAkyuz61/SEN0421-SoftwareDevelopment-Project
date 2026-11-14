using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Kaynak bazlı uygunsuzluk raporunun hesabı: kırılım, ortalama kapanış
// süresi, termin uyumu ve açık / kapalı dağılımı (md. 9.1.3).
public class NonconformityReportTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

    private static NonconformityReportRow Row(
        int id, NonconformitySource source, NonconformityStatus status,
        double detectedDaysAgo, double? dueInDays = null, double? closedDaysAgo = null,
        bool verified = false, int evidence = 0) =>
        new(id, source, status,
            Now.AddDays(-detectedDaysAgo),
            dueInDays == null ? null : Now.AddDays(dueInDays.Value),
            closedDaysAgo == null ? null : Now.AddDays(-closedDaysAgo.Value),
            verified, evidence, "Ayşe Yılmaz", null, null, "Örnek uygunsuzluk");

    // SLA ihlali: 2 kapalı (biri zamanında, biri gecikmeli), 1 açık gecikmiş
    // Düşük memnuniyet: 1 işlemde, termini gelecekte
    private static readonly List<NonconformityReportRow> Ornek = new()
    {
        Row(1, NonconformitySource.SlaBreach, NonconformityStatus.Closed,
            detectedDaysAgo: 10, dueInDays: -3, closedDaysAgo: 6, verified: true, evidence: 2),
        Row(2, NonconformitySource.SlaBreach, NonconformityStatus.Closed,
            detectedDaysAgo: 20, dueInDays: -13, closedDaysAgo: 4, verified: true, evidence: 1),
        Row(3, NonconformitySource.SlaBreach, NonconformityStatus.Open,
            detectedDaysAgo: 9, dueInDays: -2),
        Row(4, NonconformitySource.LowSatisfaction, NonconformityStatus.InProgress,
            detectedDaysAgo: 1, dueInDays: 6)
    };

    private static NonconformitySourceStat Stat(NonconformitySource? s) =>
        NonconformityReportPolicy.Summarize(Ornek, Now).First(x => x.Source == s);

    [Fact]
    public void Her_kaynak_icin_satir_ve_sonda_toplam_bulunur()
    {
        var stats = NonconformityReportPolicy.Summarize(Ornek, Now);

        // 4 kaynak + TOPLAM
        Assert.Equal(Enum.GetValues<NonconformitySource>().Length + 1, stats.Count);
        Assert.Null(stats[^1].Source);

        // Kaydı olmayan kaynak da listede kalır: "hiç uygunsuzluk doğurmamış"
        // bilgisi, satırın görünmemesinden farklıdır
        Assert.Equal(0, Stat(NonconformitySource.Audit).Total);
    }

    [Fact]
    public void Acik_kapali_dagilimi_kaynak_bazinda_sayilir()
    {
        var sla = Stat(NonconformitySource.SlaBreach);

        Assert.Equal(3, sla.Total);
        Assert.Equal(1, sla.Open);
        Assert.Equal(0, sla.InProgress);
        Assert.Equal(2, sla.Closed);

        var memnuniyet = Stat(NonconformitySource.LowSatisfaction);
        Assert.Equal(1, memnuniyet.InProgress);
        Assert.Equal(0, memnuniyet.Closed);
    }

    [Fact]
    public void Ortalama_kapanis_suresi_yalnizca_kapanan_kayitlardan_hesaplanir()
    {
        // #1: 10 gün önce tespit, 6 gün önce kapandı → 4 gün
        // #2: 20 gün önce tespit, 4 gün önce kapandı → 16 gün
        var sla = Stat(NonconformitySource.SlaBreach);

        Assert.NotNull(sla.AvgClosureDays);
        Assert.Equal(10.0, sla.AvgClosureDays!.Value, precision: 3);

        // Hiç kapanmamış kaynakta ortalama YOK (0 değil): sıfır gün ortalama,
        // "hemen kapanıyor" gibi okunurdu
        Assert.Null(Stat(NonconformitySource.LowSatisfaction).AvgClosureDays);
    }

    [Fact]
    public void Termin_uyumu_zamaninda_ve_geciken_kapanislardan_cikar()
    {
        var sla = Stat(NonconformitySource.SlaBreach);

        // #1 termininden (3 gün önce) daha önce kapandı (6 gün önce) → zamanında
        // #2 termini 13 gün önceydi, 4 gün önce kapandı → gecikmeli
        Assert.Equal(1, sla.ClosedOnTime);
        Assert.Equal(1, sla.ClosedLate);
        Assert.Equal(2, sla.OnTimeMeasured);
        Assert.Equal(50.0, sla.OnTimeRate!.Value, precision: 3);
    }

    [Fact]
    public void Termini_olmayan_kapanan_kayit_termin_uyumu_olcusune_girmez()
    {
        var rows = new List<NonconformityReportRow>
        {
            Row(1, NonconformitySource.Manual, NonconformityStatus.Closed,
                detectedDaysAgo: 5, dueInDays: null, closedDaysAgo: 1)
        };

        var stat = NonconformityReportPolicy.Summarize(rows, Now).First(s => s.Source == NonconformitySource.Manual);

        Assert.Equal(1, stat.Closed);
        Assert.Equal(0, stat.OnTimeMeasured);
        Assert.Null(stat.OnTimeRate);          // ölçülemeyen oran 0% diye gösterilmez
        Assert.NotNull(stat.AvgClosureDays);   // kapanış süresi yine ölçülür
    }

    [Fact]
    public void Termini_gecen_acik_kayitlar_ayrica_sayilir()
    {
        var sla = Stat(NonconformitySource.SlaBreach);
        Assert.Equal(1, sla.OverdueOpen);      // #3: açık ve termini 2 gün önce geçti

        // İşlemde ama termini gelecekte olan kayıt gecikmiş sayılmaz
        Assert.Equal(0, Stat(NonconformitySource.LowSatisfaction).OverdueOpen);
    }

    [Fact]
    public void Toplam_satiri_butun_kaynaklari_birlestirir()
    {
        var toplam = Stat(null);

        Assert.Equal(4, toplam.Total);
        Assert.Equal(2, toplam.Closed);
        Assert.Equal(2, toplam.Verified);
        Assert.Equal(1, toplam.OverdueOpen);
        Assert.Equal(50.0, toplam.ClosedRate, precision: 3);
    }

    [Fact]
    public void Kapanis_suresi_negatif_olmaz()
    {
        // Bozuk veri: kapanış tespitten önce yazılmış
        var r = Row(1, NonconformitySource.Manual, NonconformityStatus.Closed,
            detectedDaysAgo: 2, closedDaysAgo: 5);

        Assert.Equal(0, r.ClosureDays);
    }

    [Fact]
    public void Kayit_yoksa_rapor_bos_degerlerle_doner()
    {
        var stats = NonconformityReportPolicy.Summarize(new List<NonconformityReportRow>(), Now);
        var toplam = stats[^1];

        Assert.Equal(0, toplam.Total);
        Assert.Null(toplam.AvgClosureDays);
        Assert.Null(toplam.OnTimeRate);
        Assert.Equal(0, toplam.ClosedRate);
    }
}
