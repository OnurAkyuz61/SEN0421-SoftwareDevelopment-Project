using System.Globalization;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Yönetim gözden geçirme (ISO 9001 md. 9.3) raporunun dönemi.
//
// Dönem YEREL gün sınırlarıyla seçilir (kullanıcı "1–30 Kasım" der), sorgular
// UTC ile çalışır. Karşılaştırma dönemi, seçilen dönemin HEMEN ÖNCESİNDEKİ
// eşit uzunlukta penceredir: "geçen çeyreğe göre" sorusu md. 9.3.2'nin
// istediği eğilim bilgisidir.
public sealed record ManagementReviewPeriod(DateTime FromLocal, DateTime ToLocal)
{
    // Bitiş günü dönemin İÇİNDE: kullanıcı 30 Kasım'ı seçtiyse o gün de sayılır
    public int Days => Math.Max(1, (ToLocal.Date - FromLocal.Date).Days + 1);

    public DateTime FromUtc => AppTime.ToUtc(FromLocal.Date);
    public DateTime ToUtc => AppTime.ToUtc(ToLocal.Date.AddDays(1));

    // Önceki eşit uzunlukta pencere (bitişi bu dönemin başlangıcının bir gün öncesi)
    public ManagementReviewPeriod Previous() =>
        new(FromLocal.Date.AddDays(-Days), FromLocal.Date.AddDays(-1));

    public string Label => $"{FromLocal:dd.MM.yyyy} – {ToLocal:dd.MM.yyyy}";
}

// İki dönemin aynı göstergesi. Yönün "iyi" mi olduğu göstergeye göre değişir:
// SLA uyumunun artması iyidir, ortalama çözüm süresinin artması kötüdür.
public sealed record ManagementReviewDelta(decimal Current, decimal Previous, bool HigherIsBetter)
{
    public decimal Change => Current - Previous;

    // Önceki dönem sıfırsa yüzde değişim tanımsız: "%∞ artış" yazmak yerine
    // raporda yalnızca mutlak değişim gösterilir
    public decimal? ChangePercent => Previous == 0 ? null : Math.Round(100m * Change / Math.Abs(Previous), 1);

    public bool Improved => Change != 0 && (HigherIsBetter ? Change > 0 : Change < 0);
    public bool Worsened => Change != 0 && !Improved;
}

// Dönemdeki KPI snapshot'larının toplamı (md. 9.1.1 → 9.3.2 c).
//
// SnapshotDays raporun KENDİ güvenilirliğini söyler: göstergeler günlük
// snapshot'lardan toplanıyor ve snapshot yalnızca uygulamanın çalıştığı
// günler için üretiliyor (KpiAggregatorService). Dönemde 90 gün varken 12
// snapshot bulunduysa sayılar dönemi temsil etmez; yönetim bunu görmeli.
public sealed record KpiAggregate(
    int SnapshotDays,
    int TotalTickets, int ResolvedTickets, int BreachedTickets, int ReopenCount,
    int FirstResponseBreachedTickets,
    decimal SlaComplianceRate, decimal FirstResponseComplianceRate,
    decimal AvgResolutionHours, decimal AvgFirstResponseHours,
    decimal CsatAverage, int SurveyResponseCount, decimal CsatResponseRate, int LowCsatCount)
{
    public static readonly KpiAggregate Empty =
        new(0, 0, 0, 0, 0, 0, 100m, 100m, 0m, 0m, 0m, 0, 0m, 0);

    public bool HasData => SnapshotDays > 0;
}

// Anket sonucunun soru bazında kırılımı: hangi boyut zayıf (md. 9.1.2).
public sealed record SurveyDimension(int QuestionId, string Question, int AnswerCount, double Average)
{
    public bool IsWeak(double threshold) => AnswerCount > 0 && Average < threshold;
}

// CSAT eğiliminin tek noktası (aylık kova)
public sealed record CsatTrendPoint(DateTime MonthLocal, int ResponseCount, double Average, int LowCount)
{
    public string Label => MonthLocal.ToString("MM.yyyy");
}

public sealed record SurveySection(
    int ResponseCount, int AnswerCount, double Average, int LowCount, int CommentCount,
    int ResolvedTickets, decimal ResponseRate,
    List<SurveyDimension> Dimensions, List<CsatTrendPoint> Trend)
{
    public double? WeakestAverage => Dimensions.Count == 0 ? null : Dimensions.Min(d => d.Average);
}

// Bilgi bankası satırı (en çok kullanılan / gözden geçirilmesi gereken)
public sealed record ArticleLine(
    int Id, string Title, int UsedCount, DateTime? LastUsedAt,
    int HelpfulCount, int NotHelpfulCount, KnowledgeUsageIssue Issues)
{
    public IReadOnlyList<string> Reasons => KnowledgeUsagePolicy.Describe(Issues);
}

public sealed record KnowledgeSection(
    int PublishedTotal, int DraftTotal, int PendingApprovalTotal, int RejectedTotal,
    int CreatedInPeriod, int TicketsSolvedByArticle,
    int NeverUsed, int Stale, int Negative,
    List<ArticleLine> TopUsed, List<ArticleLine> NeedsReview,
    int UnansweredQuestionCount, int UnansweredAskTotal, List<ChatbotQuestion> TopQuestions);

// Depolama ve saklama (md. 7.5.3: dokümante edilmiş bilginin korunması ve
// elden çıkarılması tanımlı olmalıdır).
public sealed record StorageSection(
    int TicketFileCount, long TicketFileBytes,
    int EvidenceFileCount, long EvidenceFileBytes,
    int UploadedInPeriod, long UploadedInPeriodBytes,
    int RemovedPendingPurge, long RemovedPendingPurgeBytes, int PurgeDueCount,
    int SuspiciousCount, int FailedScanCount, int PendingScanCount,
    bool RetentionEnabled, int RetentionDays, bool OrphanScanEnabled, bool OrphanDeletesFiles,
    long TicketQuotaBytes, long UserQuotaBytes)
{
    public int TotalFileCount => TicketFileCount + EvidenceFileCount;
    public long TotalBytes => TicketFileBytes + EvidenceFileBytes;

    // Taramadan temiz geçmemiş dosya: denetimde açıklanması gereken kalem
    public int NotCleanCount => SuspiciousCount + FailedScanCount + PendingScanCount;
}

// Eskalasyon ve onay istatistikleri (md. 9.3.2 c: süreç performansı)
public sealed record OperationsSection(
    int EscalationTotal, int EscalationManual, int EscalationAutomatic, int EscalationFallback,
    int EscalatedTickets, double AvgMinutesOverdue, List<(int Level, int Count)> EscalationByLevel,
    int ChangeTotal, int ApprovalPending, int ApprovalApproved, int ApprovalRejected,
    int ApprovalDecisions, int DelegatedDecisions, int ResubmittedChanges, double? AvgApprovalHours)
{
    public int ApprovalDecided => ApprovalApproved + ApprovalRejected;

    // Onay oranı: karara bağlanmış değişikliklerin kaçı onaylandı
    public double? ApprovalRate => ApprovalDecided == 0 ? null : 100.0 * ApprovalApproved / ApprovalDecided;
}

// Yönetim gözden geçirme paketinin tamamı. YALNIZCA OKUMA: hiçbir bölüm yeni
// kayıt üretmez, var olan ölçüm ve kayıtları tek dönemsel tabloda toplar.
public sealed record ManagementReviewReport(
    ManagementReviewPeriod Period,
    ManagementReviewPeriod PreviousPeriod,
    KpiAggregate Kpi,
    KpiAggregate PreviousKpi,
    List<NonconformitySourceStat> Nonconformities,
    SurveySection Survey,
    KnowledgeSection Knowledge,
    StorageSection Storage,
    OperationsSection Operations,
    DateTime GeneratedAt)
{
    // TOPLAM satırı (NonconformityReportPolicy.Summarize sonda üretir)
    public NonconformitySourceStat NonconformityTotal => Nonconformities[^1];

    public IEnumerable<NonconformitySourceStat> NonconformityBySource =>
        Nonconformities.Take(Nonconformities.Count - 1);
}

// Yönetim gözden geçirme raporunun HESABI.
//
// ISO 9001 md. 9.3.2 gözden geçirmenin belirli girdileri kapsamasını ister:
// süreç performansı ve göstergeler, müşteri memnuniyeti, uygunsuzluklar ve
// düzeltici faaliyetler, kaynakların yeterliliği. Bu girdiler sistemde
// zaten ayrı ekranlarda duruyordu; paket onları tek dönemsel sayfada
// birleştirir — yeni veri üretmeden.
//
// Saf sınıf: veritabanına ve HTTP'ye bağımlı değil, birim testi yazılabiliyor.
// Sorgular ManagementReviewService'te.
public static class ManagementReviewPolicy
{
    // ───── KPI toplama ───────────────────────────────────────────────────

    // Günlük snapshot'ları tek döneme indirger.
    //
    // Oranlar snapshot'lardaki yüzdelerin düz ortalaması DEĞİL: her gün aynı
    // ağırlıkta sayılırsa 2 talepli bir cumartesi, 80 talepli bir salıyla eşit
    // etki eder. Sayılabilen göstergeler toplamlardan YENİDEN hesaplanır
    // (KpiService'teki formülün aynısı), geri kalanlar ilgili sayıya göre
    // AĞIRLIKLI ortalamadır:
    //   • Ortalama çözüm süresi: ağırlık çözülen talep sayısı — snapshot'lar
    //     talepleri açılış gününe göre böldüğü için sonuç TAM ortalamadır.
    //   • Ortalama ilk yanıt ve CSAT: ağırlık talep / yanıt sayısı; ölçümün
    //     paydası (yanıt veren talep sayısı, cevap sayısı) snapshot'ta
    //     tutulmadığı için bu ikisi yaklaşıktır. Raporda yaklaşık olduğu yazılı.
    public static KpiAggregate AggregateKpi(IReadOnlyCollection<KpiSnapshot> snapshots)
    {
        if (snapshots.Count == 0) return KpiAggregate.Empty;

        var total = snapshots.Sum(s => s.TotalTickets);
        var resolved = snapshots.Sum(s => s.ResolvedTickets);
        var breached = snapshots.Sum(s => s.BreachedTickets);
        var frBreached = snapshots.Sum(s => s.FirstResponseBreachedTickets);
        var responses = snapshots.Sum(s => s.SurveyResponseCount);

        return new KpiAggregate(
            SnapshotDays: snapshots.Count,
            TotalTickets: total,
            ResolvedTickets: resolved,
            BreachedTickets: breached,
            ReopenCount: snapshots.Sum(s => s.ReopenCount),
            FirstResponseBreachedTickets: frBreached,
            SlaComplianceRate: total == 0 ? 100m : Round1(100m * (total - breached) / total),
            FirstResponseComplianceRate: Weighted(snapshots, s => s.FirstResponseComplianceRate, s => s.TotalTickets, 100m),
            AvgResolutionHours: Weighted(snapshots, s => s.AvgResolutionHours, s => s.ResolvedTickets, 0m),
            AvgFirstResponseHours: Weighted(snapshots, s => s.AvgFirstResponseHours, s => s.TotalTickets, 0m),
            CsatAverage: Weighted(snapshots, s => s.CsatAverage, s => s.SurveyResponseCount, 0m, decimals: 2),
            SurveyResponseCount: responses,
            // Yanıt oranının paydası çözülen talep sayısı; %100'de sınırlanır
            // (KpiService ile aynı gerekçe: yeniden açılan talep yanıtını korur)
            CsatResponseRate: resolved == 0 ? 0m : Math.Min(100m, Round1(100m * responses / resolved)),
            LowCsatCount: snapshots.Sum(s => s.LowCsatCount));
    }

    // Ağırlığı sıfır olan günler ölçüye girmez: o gün hiç çözülen talep
    // yoksa "0 saat çözüm süresi" ortalamayı aşağı çekmemeli
    private static decimal Weighted(IEnumerable<KpiSnapshot> snapshots,
                                    Func<KpiSnapshot, decimal> value, Func<KpiSnapshot, int> weight,
                                    decimal fallback, int decimals = 1)
    {
        var rows = snapshots.Where(s => weight(s) > 0).ToList();
        if (rows.Count == 0) return fallback;

        var w = rows.Sum(s => (decimal)weight(s));
        return Math.Round(rows.Sum(s => value(s) * weight(s)) / w, decimals);
    }

    private static decimal Round1(decimal v) => Math.Round(v, 1);

    // ───── Anket ─────────────────────────────────────────────────────────

    // Zayıf boyutlar: eşiğin altında kalan sorular, en düşük puan başta.
    // Eşik uygunsuzluk eşiğiyle aynı (Quality:LowSatisfactionThreshold):
    // raporda "zayıf" görünen boyut ile kalite kaydı doğuran puan birbirini
    // tutsun.
    public static List<SurveyDimension> WeakDimensions(IEnumerable<SurveyDimension> dimensions, double threshold) =>
        dimensions.Where(d => d.IsWeak(threshold)).OrderBy(d => d.Average).ToList();

    // ───── CSV ───────────────────────────────────────────────────────────

    public sealed record CsvRow(string Section, string Metric, string Value,
                                string Previous = "", string Change = "");

    // Rapor karışık yapıda (bölüm + gösterge + değer) olduğu için CSV uzun
    // biçimde üretilir: tek bir tabloya sığmayan bölümler yan yana değil
    // alt alta durur, Excel'de süzülebilir.
    public static List<CsvRow> CsvRows(ManagementReviewReport r)
    {
        var rows = new List<CsvRow>();
        var inv = CultureInfo.InvariantCulture;

        string N(int v) => v.ToString(inv);
        string D(decimal v, string f = "0.0") => v.ToString(f, inv);
        string Dd(double? v, string f = "0.0") => v == null ? "" : v.Value.ToString(f, inv);

        void Kpi(string metric, decimal cur, decimal prev, bool higherIsBetter, string f = "0.0")
        {
            var d = new ManagementReviewDelta(cur, prev, higherIsBetter);
            rows.Add(new CsvRow("KPI", metric, D(cur, f), D(prev, f), D(d.Change, f)));
        }

        // ── Dönem ──
        rows.Add(new CsvRow("Dönem", "Gözden geçirme dönemi", r.Period.Label, r.PreviousPeriod.Label));
        rows.Add(new CsvRow("Dönem", "Gün sayısı", N(r.Period.Days), N(r.PreviousPeriod.Days)));
        rows.Add(new CsvRow("Dönem", "KPI snapshot günü", N(r.Kpi.SnapshotDays), N(r.PreviousKpi.SnapshotDays)));
        rows.Add(new CsvRow("Dönem", "Rapor zamanı",
            AppTime.ToLocal(r.GeneratedAt).ToString("dd.MM.yyyy HH:mm", inv)));

        // ── KPI ──
        Kpi("Toplam talep", r.Kpi.TotalTickets, r.PreviousKpi.TotalTickets, true, "0");
        Kpi("Çözülen talep", r.Kpi.ResolvedTickets, r.PreviousKpi.ResolvedTickets, true, "0");
        Kpi("SLA uyumu (%)", r.Kpi.SlaComplianceRate, r.PreviousKpi.SlaComplianceRate, true);
        Kpi("SLA ihlali", r.Kpi.BreachedTickets, r.PreviousKpi.BreachedTickets, false, "0");
        Kpi("İlk yanıt SLA uyumu (%)", r.Kpi.FirstResponseComplianceRate, r.PreviousKpi.FirstResponseComplianceRate, true);
        Kpi("Ortalama çözüm (saat)", r.Kpi.AvgResolutionHours, r.PreviousKpi.AvgResolutionHours, false);
        Kpi("Ortalama ilk yanıt (saat)", r.Kpi.AvgFirstResponseHours, r.PreviousKpi.AvgFirstResponseHours, false);
        Kpi("Yeniden açılma", r.Kpi.ReopenCount, r.PreviousKpi.ReopenCount, false, "0");
        Kpi("Memnuniyet (CSAT)", r.Kpi.CsatAverage, r.PreviousKpi.CsatAverage, true, "0.00");
        Kpi("Anket yanıtı", r.Kpi.SurveyResponseCount, r.PreviousKpi.SurveyResponseCount, true, "0");
        Kpi("Anket yanıt oranı (%)", r.Kpi.CsatResponseRate, r.PreviousKpi.CsatResponseRate, true);
        Kpi("Düşük puanlı yanıt", r.Kpi.LowCsatCount, r.PreviousKpi.LowCsatCount, false, "0");

        // ── Uygunsuzluk (kaynak bazlı) ──
        foreach (var s in r.Nonconformities)
        {
            var ad = s.Source == null ? "TOPLAM" : Ui.NonconformitySourceText(s.Source.Value);
            rows.Add(new CsvRow("Uygunsuzluk", $"{ad} · toplam", N(s.Total)));
            rows.Add(new CsvRow("Uygunsuzluk", $"{ad} · açık / işlemde", $"{N(s.Open)} / {N(s.InProgress)}"));
            rows.Add(new CsvRow("Uygunsuzluk", $"{ad} · kapalı", N(s.Closed)));
            rows.Add(new CsvRow("Uygunsuzluk", $"{ad} · ortalama kapanış (gün)", Dd(s.AvgClosureDays)));
            rows.Add(new CsvRow("Uygunsuzluk", $"{ad} · termin uyumu (%)", Dd(s.OnTimeRate)));
            rows.Add(new CsvRow("Uygunsuzluk", $"{ad} · termini geçen açık", N(s.OverdueOpen)));
            rows.Add(new CsvRow("Uygunsuzluk", $"{ad} · etkinlik doğrulama (%)", Dd(s.VerificationRate)));
        }

        // ── Anket ──
        var sv = r.Survey;
        rows.Add(new CsvRow("Anket", "Yanıt sayısı", N(sv.ResponseCount)));
        rows.Add(new CsvRow("Anket", "Ortalama puan", Dd(sv.Average, "0.00")));
        rows.Add(new CsvRow("Anket", "Eşik altı yanıt", N(sv.LowCount)));
        rows.Add(new CsvRow("Anket", "Yanıt oranı (%)", D(sv.ResponseRate)));
        rows.Add(new CsvRow("Anket", "Yorum yazan", N(sv.CommentCount)));
        foreach (var d in sv.Dimensions)
            rows.Add(new CsvRow("Anket", $"Soru · {d.Question}", Dd(d.Average, "0.00"), "", N(d.AnswerCount)));
        foreach (var t in sv.Trend)
            rows.Add(new CsvRow("Anket eğilimi", t.Label, Dd(t.Average, "0.00"), "", N(t.ResponseCount)));

        // ── Bilgi bankası ──
        var kb = r.Knowledge;
        rows.Add(new CsvRow("Bilgi bankası", "Yayında makale", N(kb.PublishedTotal)));
        rows.Add(new CsvRow("Bilgi bankası", "Taslak / onay bekleyen / reddedilen",
            $"{N(kb.DraftTotal)} / {N(kb.PendingApprovalTotal)} / {N(kb.RejectedTotal)}"));
        rows.Add(new CsvRow("Bilgi bankası", "Dönemde yazılan", N(kb.CreatedInPeriod)));
        rows.Add(new CsvRow("Bilgi bankası", "Makaleyle çözülen talep", N(kb.TicketsSolvedByArticle)));
        rows.Add(new CsvRow("Bilgi bankası", "Hiç kullanılmamış", N(kb.NeverUsed)));
        rows.Add(new CsvRow("Bilgi bankası", "Kullanımı eskimiş", N(kb.Stale)));
        rows.Add(new CsvRow("Bilgi bankası", "Olumsuz geri bildirimli", N(kb.Negative)));
        foreach (var a in kb.TopUsed)
            rows.Add(new CsvRow("Bilgi bankası · en çok kullanılan", a.Title, N(a.UsedCount)));
        foreach (var a in kb.NeedsReview)
            rows.Add(new CsvRow("Bilgi bankası · gözden geçir", a.Title, N(a.UsedCount), "",
                string.Join(" · ", a.Reasons)));
        rows.Add(new CsvRow("Chatbot", "Cevaplanamayan soru", N(kb.UnansweredQuestionCount)));
        rows.Add(new CsvRow("Chatbot", "Cevaplanamayan sorulma sayısı", N(kb.UnansweredAskTotal)));
        foreach (var q in kb.TopQuestions)
            rows.Add(new CsvRow("Chatbot · en çok sorulan", q.Text, N(q.AskCount)));

        // ── Depolama ──
        var st = r.Storage;
        rows.Add(new CsvRow("Depolama", "Talep eki (dosya / bayt)", $"{N(st.TicketFileCount)} / {N64(st.TicketFileBytes)}"));
        rows.Add(new CsvRow("Depolama", "Uygunsuzluk kanıtı (dosya / bayt)", $"{N(st.EvidenceFileCount)} / {N64(st.EvidenceFileBytes)}"));
        rows.Add(new CsvRow("Depolama", "Toplam (dosya / bayt)", $"{N(st.TotalFileCount)} / {N64(st.TotalBytes)}"));
        rows.Add(new CsvRow("Depolama", "Dönemde yüklenen (dosya / bayt)", $"{N(st.UploadedInPeriod)} / {N64(st.UploadedInPeriodBytes)}"));
        rows.Add(new CsvRow("Depolama", "Kaldırılmış, temizlik bekleyen", N(st.RemovedPendingPurge)));
        rows.Add(new CsvRow("Depolama", "Saklama süresi dolmuş (silinecek)", N(st.PurgeDueCount)));
        rows.Add(new CsvRow("Depolama", "Şüpheli / taranamamış / taranmamış",
            $"{N(st.SuspiciousCount)} / {N(st.FailedScanCount)} / {N(st.PendingScanCount)}"));
        rows.Add(new CsvRow("Saklama politikası", "Temizlik açık", st.RetentionEnabled ? "Evet" : "Hayır"));
        rows.Add(new CsvRow("Saklama politikası", "Saklama süresi (gün)", N(st.RetentionDays)));
        rows.Add(new CsvRow("Saklama politikası", "Sahipsiz dosya taraması",
            st.OrphanScanEnabled ? (st.OrphanDeletesFiles ? "Açık (siliyor)" : "Açık (yalnızca rapor)") : "Kapalı"));

        // ── Eskalasyon ve onay ──
        var op = r.Operations;
        rows.Add(new CsvRow("Eskalasyon", "Toplam", N(op.EscalationTotal)));
        rows.Add(new CsvRow("Eskalasyon", "Manuel / otomatik", $"{N(op.EscalationManual)} / {N(op.EscalationAutomatic)}"));
        rows.Add(new CsvRow("Eskalasyon", "Etkilenen talep", N(op.EscalatedTickets)));
        rows.Add(new CsvRow("Eskalasyon", "Yedek kurala düşen", N(op.EscalationFallback)));
        rows.Add(new CsvRow("Eskalasyon", "Ortalama gecikme (dk)", Dd(op.AvgMinutesOverdue, "0")));
        foreach (var (level, count) in op.EscalationByLevel)
            rows.Add(new CsvRow("Eskalasyon", $"Seviye {N(level)}", N(count)));
        rows.Add(new CsvRow("Onay", "Değişiklik talebi", N(op.ChangeTotal)));
        rows.Add(new CsvRow("Onay", "Bekleyen", N(op.ApprovalPending)));
        rows.Add(new CsvRow("Onay", "Onaylanan / reddedilen", $"{N(op.ApprovalApproved)} / {N(op.ApprovalRejected)}"));
        rows.Add(new CsvRow("Onay", "Onay oranı (%)", Dd(op.ApprovalRate)));
        rows.Add(new CsvRow("Onay", "Verilen karar", N(op.ApprovalDecisions)));
        rows.Add(new CsvRow("Onay", "Vekaleten karar", N(op.DelegatedDecisions)));
        rows.Add(new CsvRow("Onay", "Yeniden gönderilen", N(op.ResubmittedChanges)));
        rows.Add(new CsvRow("Onay", "Ortalama karar süresi (saat)", Dd(op.AvgApprovalHours)));

        return rows;

        string N64(long v) => v.ToString(inv);
    }
}
