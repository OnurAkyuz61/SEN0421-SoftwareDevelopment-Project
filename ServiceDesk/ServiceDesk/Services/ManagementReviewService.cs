using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Yönetim gözden geçirme paketini (md. 9.3) kurar.
//
// YALNIZCA OKUMA: tüm sorgular AsNoTracking, hiçbir yerde SaveChanges yok,
// yeni kayıt üretilmez. Rapor var olan ölçümleri (KPI snapshot, uygunsuzluk,
// anket, bilgi bankası, ek dosya, eskalasyon, onay) tek dönemsel sayfada
// birleştirir.
//
// Hesaplar saf sınıflarda: ManagementReviewPolicy (KPI toplama, zayıf
// boyutlar, CSV) ve NonconformityReportPolicy (kaynak kırılımı). Burada
// yalnızca sorgu var — rapor ekranı ile CSV aynı kaynaktan beslenir, ikisi
// birbirinden ayrı düşemez.
public class ManagementReviewService
{
    // Listelerde gösterilecek satır sayısı: rapor tek sayfada yazdırılabilsin
    private const int TopCount = 10;

    private readonly AppDbContext _db;
    private readonly QualitySettings _quality;
    private readonly UploadSettings _uploads;

    public ManagementReviewService(AppDbContext db, IOptions<QualitySettings> quality,
                                   IOptions<UploadSettings> uploads)
    {
        _db = db;
        _quality = quality.Value;
        _uploads = uploads.Value;
    }

    public double LowSatisfactionThreshold => _quality.LowSatisfactionThreshold;

    public async Task<ManagementReviewReport> BuildAsync(ManagementReviewPeriod period,
                                                         CancellationToken token = default)
    {
        var previous = period.Previous();
        var now = AppTime.UtcNow;

        return new ManagementReviewReport(
            Period: period,
            PreviousPeriod: previous,
            Kpi: await KpiAsync(period, token),
            PreviousKpi: await KpiAsync(previous, token),
            Nonconformities: await NonconformitiesAsync(period, now, token),
            Survey: await SurveyAsync(period, token),
            Knowledge: await KnowledgeAsync(period, now, token),
            Storage: await StorageAsync(period, now, token),
            Operations: await OperationsAsync(period, token),
            GeneratedAt: now);
    }

    // ───── KPI (md. 9.1.1) ───────────────────────────────────────────────

    // Kurum geneli GÜNLÜK snapshot'lar (DepartmentId == null). Aylık
    // snapshot'lar alınmaz: ikisi birlikte toplanırsa aynı talep iki kez
    // sayılır.
    private async Task<KpiAggregate> KpiAsync(ManagementReviewPeriod p, CancellationToken token)
    {
        var snapshots = await _db.KpiSnapshots.AsNoTracking()
            .Where(k => k.Period == KpiPeriod.Daily && k.DepartmentId == null
                        && k.PeriodStart >= p.FromUtc && k.PeriodStart < p.ToUtc)
            .ToListAsync(token);

        return ManagementReviewPolicy.AggregateKpi(snapshots);
    }

    // ───── Uygunsuzluk (md. 10.2) ────────────────────────────────────────

    // Kırılım, uygunsuzluk raporunun kullandığı saf sınıfın aynısıyla
    // hesaplanır: iki ekranda farklı sayı görünmesin.
    private async Task<List<NonconformitySourceStat>> NonconformitiesAsync(
        ManagementReviewPeriod p, DateTime now, CancellationToken token)
    {
        var rows = await _db.Nonconformities.AsNoTracking()
            .Where(n => n.DetectedAt >= p.FromUtc && n.DetectedAt < p.ToUtc)
            .Select(n => new NonconformityReportRow(
                n.Id, n.Source, n.Status, n.DetectedAt, n.DueAt, n.ClosedAt,
                n.EffectivenessVerified == true && n.VerifiedById != null,
                n.Attachments.Count,
                n.Responsible == null ? null : n.Responsible.FirstName + " " + n.Responsible.LastName,
                n.TicketId,
                n.Ticket == null ? null : n.Ticket.Number,
                n.Description))
            .ToListAsync(token);

        return NonconformityReportPolicy.Summarize(rows, now);
    }

    // ───── Anket ve CSAT (md. 9.1.2) ─────────────────────────────────────

    private async Task<SurveySection> SurveyAsync(ManagementReviewPeriod p, CancellationToken token)
    {
        // Yanıtın kendisi dönem içinde doldurulmuş olmalı: talebin açılış
        // tarihine göre süzmek, dönem sonunda gelen değerlendirmeleri
        // raporun dışında bırakırdı.
        var answers = await _db.SurveyAnswers.AsNoTracking()
            .Where(a => a.Response!.CreatedAt >= p.FromUtc && a.Response.CreatedAt < p.ToUtc)
            .Select(a => new
            {
                a.ResponseId,
                a.Score,
                a.QuestionId,
                Question = a.Question!.Text,
                Created = a.Response!.CreatedAt
            })
            .ToListAsync(token);

        var responseCount = await _db.SurveyResponses.AsNoTracking()
            .CountAsync(r => r.CreatedAt >= p.FromUtc && r.CreatedAt < p.ToUtc, token);

        var commentCount = await _db.SurveyResponses.AsNoTracking()
            .CountAsync(r => r.CreatedAt >= p.FromUtc && r.CreatedAt < p.ToUtc
                             && r.Comment != null && r.Comment != "", token);

        // Yanıt oranının paydası dönemde ÇÖZÜLEN talep (KpiService ile aynı
        // tanım): anket yalnızca çözülen talep için sorulur
        var resolved = await _db.Tickets.AsNoTracking()
            .CountAsync(t => t.ResolvedAt != null && t.ResolvedAt >= p.FromUtc && t.ResolvedAt < p.ToUtc, token);

        // Yanıt bazında ortalama: "eşik altı yanıt" sayısı tek cevaptan değil
        // yanıtın kendi ortalamasından çıkar (NonconformityService ile aynı kural)
        var perResponse = answers
            .GroupBy(a => a.ResponseId)
            .Select(g => new { Ortalama = g.Average(a => a.Score), Ay = AyBasi(g.First().Created) })
            .ToList();

        var dimensions = answers
            .GroupBy(a => new { a.QuestionId, a.Question })
            .Select(g => new SurveyDimension(g.Key.QuestionId, g.Key.Question, g.Count(), g.Average(a => a.Score)))
            .OrderBy(d => d.Average)
            .ToList();

        // Eğilim: aylık kovalar, dönem içinde kronolojik
        var trend = perResponse
            .GroupBy(r => r.Ay)
            .OrderBy(g => g.Key)
            .Select(g => new CsatTrendPoint(
                g.Key, g.Count(), g.Average(r => r.Ortalama),
                g.Count(r => r.Ortalama < _quality.LowSatisfactionThreshold)))
            .ToList();

        return new SurveySection(
            ResponseCount: responseCount,
            AnswerCount: answers.Count,
            Average: answers.Count == 0 ? 0 : answers.Average(a => a.Score),
            LowCount: perResponse.Count(r => r.Ortalama < _quality.LowSatisfactionThreshold),
            CommentCount: commentCount,
            ResolvedTickets: resolved,
            // %100'de sınırlanır: değerlendirildikten sonra yeniden açılan
            // talep yanıtı korur ama ResolvedAt'i boşalır
            ResponseRate: resolved == 0 ? 0m : Math.Min(100m, Math.Round(100m * responseCount / resolved, 1)),
            Dimensions: dimensions,
            Trend: trend);
    }

    // Yerel ayın ilk günü: eğilim kovaları kullanıcının gördüğü takvime göre
    private static DateTime AyBasi(DateTime utc)
    {
        var local = AppTime.ToLocal(utc);
        return new DateTime(local.Year, local.Month, 1);
    }

    // ───── Bilgi bankası (md. 7.5 / 10.3) ────────────────────────────────

    private async Task<KnowledgeSection> KnowledgeAsync(
        ManagementReviewPeriod p, DateTime now, CancellationToken token)
    {
        // Yayındaki makaleler küçük bir projeksiyonla okunur; sınıflandırma
        // kullanılmayan makale raporunun kullandığı saf sınıfın aynısı
        var published = await _db.KnowledgeArticles.AsNoTracking()
            .Where(a => a.Status == KnowledgeArticleStatus.Published)
            .Select(a => new
            {
                a.Id, a.Title, a.UsedCount, a.LastUsedAt, a.CreatedAt,
                a.HelpfulCount, a.NotHelpfulCount
            })
            .ToListAsync(token);

        var lines = published
            .Select(a => new ArticleLine(
                a.Id, a.Title, a.UsedCount, a.LastUsedAt, a.HelpfulCount, a.NotHelpfulCount,
                KnowledgeUsagePolicy.Classify(a.UsedCount, a.CreatedAt, a.LastUsedAt,
                    a.HelpfulCount, a.NotHelpfulCount, now)))
            .ToList();

        var byStatus = await _db.KnowledgeArticles.AsNoTracking()
            .GroupBy(a => a.Status)
            .Select(g => new { g.Key, C = g.Count() })
            .ToListAsync(token);

        int Durum(KnowledgeArticleStatus s) => byStatus.FirstOrDefault(x => x.Key == s)?.C ?? 0;

        var sorular = await _db.ChatbotQuestions.AsNoTracking()
            .Where(q => !q.IsResolved)
            .OrderByDescending(q => q.AskCount).ThenByDescending(q => q.LastAskedAt)
            .Take(TopCount)
            .ToListAsync(token);

        var cevapsiz = _db.ChatbotQuestions.AsNoTracking().Where(q => !q.IsResolved);

        return new KnowledgeSection(
            PublishedTotal: Durum(KnowledgeArticleStatus.Published),
            DraftTotal: Durum(KnowledgeArticleStatus.Draft),
            PendingApprovalTotal: Durum(KnowledgeArticleStatus.PendingApproval),
            RejectedTotal: Durum(KnowledgeArticleStatus.Rejected),
            CreatedInPeriod: await _db.KnowledgeArticles.AsNoTracking()
                .CountAsync(a => a.CreatedAt >= p.FromUtc && a.CreatedAt < p.ToUtc, token),
            // Dönemde makaleyle çözülen talep: bilgi bankasının gerçek faydası
            TicketsSolvedByArticle: await _db.Tickets.AsNoTracking()
                .CountAsync(t => t.ResolvedByArticleId != null
                                 && t.ResolvedAt != null && t.ResolvedAt >= p.FromUtc && t.ResolvedAt < p.ToUtc, token),
            NeverUsed: lines.Count(a => a.Issues.HasFlag(KnowledgeUsageIssue.NeverUsed)),
            Stale: lines.Count(a => a.Issues.HasFlag(KnowledgeUsageIssue.Stale)),
            Negative: lines.Count(a => a.Issues.HasFlag(KnowledgeUsageIssue.NegativeFeedback)),
            TopUsed: lines.Where(a => a.UsedCount > 0)
                .OrderByDescending(a => a.UsedCount).ThenByDescending(a => a.LastUsedAt)
                .Take(TopCount).ToList(),
            // Gözden geçirilecekler: olumsuz geri bildirim en önce (okuyucu
            // doğrudan "işime yaramadı" demiş)
            NeedsReview: lines.Where(a => a.Issues != KnowledgeUsageIssue.None)
                .OrderBy(a => a.Issues.HasFlag(KnowledgeUsageIssue.NegativeFeedback) ? 0 : 1)
                .ThenBy(a => a.UsedCount)
                .ThenBy(a => a.LastUsedAt ?? DateTime.MaxValue)
                .Take(TopCount).ToList(),
            UnansweredQuestionCount: await cevapsiz.CountAsync(token),
            UnansweredAskTotal: await cevapsiz.SumAsync(q => q.AskCount, token),
            TopQuestions: sorular);
    }

    // ───── Depolama ve saklama (md. 7.5.3) ───────────────────────────────

    private async Task<StorageSection> StorageAsync(
        ManagementReviewPeriod p, DateTime now, CancellationToken token)
    {
        // Global query filter kaldırılmış kayıtları düşürdüğü için koşullar
        // elle yazılır: "kaldırılmış ama diskte duran" dosyaları da saymak
        // raporun amacı (saklama süresi dolanlar temizlenecek olanlar).
        var files = _db.Attachments.IgnoreQueryFilters();
        var evidence = _db.NonconformityAttachments.IgnoreQueryFilters();

        var aktif = files.Where(a => !a.IsDeleted);
        var aktifKanit = evidence.Where(a => !a.IsDeleted);
        var kaldirilan = files.Where(a => a.IsDeleted);

        // Saklama süresi dolmuş: AttachmentRetentionService bir sonraki turda
        // bunları diskten ve tablodan kalıcı olarak silecek
        var esik = now.AddDays(-Math.Max(1, _uploads.Retention.Days));

        return new StorageSection(
            TicketFileCount: await aktif.CountAsync(token),
            TicketFileBytes: await aktif.SumAsync(a => a.Size, token),
            EvidenceFileCount: await aktifKanit.CountAsync(token),
            EvidenceFileBytes: await aktifKanit.SumAsync(a => a.Size, token),
            UploadedInPeriod: await aktif.CountAsync(a => a.UploadedAt >= p.FromUtc && a.UploadedAt < p.ToUtc, token)
                              + await aktifKanit.CountAsync(a => a.UploadedAt >= p.FromUtc && a.UploadedAt < p.ToUtc, token),
            UploadedInPeriodBytes: await aktif.Where(a => a.UploadedAt >= p.FromUtc && a.UploadedAt < p.ToUtc)
                                       .SumAsync(a => a.Size, token)
                                   + await aktifKanit.Where(a => a.UploadedAt >= p.FromUtc && a.UploadedAt < p.ToUtc)
                                       .SumAsync(a => a.Size, token),
            RemovedPendingPurge: await kaldirilan.CountAsync(token),
            RemovedPendingPurgeBytes: await kaldirilan.SumAsync(a => a.Size, token),
            PurgeDueCount: await kaldirilan.CountAsync(a => a.DeletedAt != null && a.DeletedAt < esik, token),
            SuspiciousCount: await aktif.CountAsync(a => a.ScanStatus == AttachmentScanStatus.Suspicious, token),
            FailedScanCount: await aktif.CountAsync(a => a.ScanStatus == AttachmentScanStatus.Failed, token),
            PendingScanCount: await aktif.CountAsync(a => a.ScanStatus == AttachmentScanStatus.Pending, token),
            RetentionEnabled: _uploads.Retention.Enabled,
            RetentionDays: _uploads.Retention.Days,
            OrphanScanEnabled: _uploads.OrphanScan.Enabled,
            OrphanDeletesFiles: _uploads.OrphanScan.DeleteFiles,
            TicketQuotaBytes: _uploads.TicketQuotaBytes,
            UserQuotaBytes: _uploads.UserQuotaBytes);
    }

    // ───── Eskalasyon ve onay (md. 9.3.2 c) ──────────────────────────────

    private async Task<OperationsSection> OperationsAsync(ManagementReviewPeriod p, CancellationToken token)
    {
        var esk = _db.EscalationRecords.AsNoTracking()
            .Where(e => e.OccurredAt >= p.FromUtc && e.OccurredAt < p.ToUtc);

        var eskToplam = await esk.CountAsync(token);

        var byLevel = (await esk.GroupBy(e => e.Level)
                .Select(g => new { g.Key, C = g.Count() })
                .OrderBy(x => x.Key).ToListAsync(token))
            .Select(x => (x.Key, x.C)).ToList();

        // Onay tarafı: dönemde AÇILAN değişiklik talepleri (onay süreci
        // talebin kendi yaşam döngüsüne ait)
        var changes = _db.Tickets.AsNoTracking()
            .Where(t => t.Type == TicketType.Change
                        && t.CreatedAt >= p.FromUtc && t.CreatedAt < p.ToUtc);

        var kararlar = _db.ChangeApprovals.AsNoTracking()
            .Where(a => a.DecidedAt >= p.FromUtc && a.DecidedAt < p.ToUtc);

        // Karara bağlanmış değişikliklerde karar süresi: açılıştan son karara.
        // İki tarih okunup fark bellekte alınıyor; süre hesabını SQL'e
        // bırakmak sağlayıcıya özel bir fonksiyona (DATEDIFF) bağımlılık
        // yaratırdı ve dönemdeki değişiklik sayısı küçüktür.
        var kararSureleri = (await changes
                .Where(t => t.ApprovalDecidedAt != null
                            && (t.ApprovalStatus == ApprovalStatus.Approved || t.ApprovalStatus == ApprovalStatus.Rejected))
                .Select(t => new { t.CreatedAt, Decided = t.ApprovalDecidedAt!.Value })
                .ToListAsync(token))
            .Select(x => (x.Decided - x.CreatedAt).TotalHours)
            .ToList();

        return new OperationsSection(
            EscalationTotal: eskToplam,
            EscalationManual: await esk.CountAsync(e => e.IsManual, token),
            EscalationAutomatic: await esk.CountAsync(e => !e.IsManual, token),
            EscalationFallback: await esk.CountAsync(e => e.UsedFallback, token),
            EscalatedTickets: await esk.Select(e => e.TicketId).Distinct().CountAsync(token),
            AvgMinutesOverdue: eskToplam == 0 ? 0 : await esk.AverageAsync(e => (double)e.MinutesOverdue, token),
            EscalationByLevel: byLevel,
            ChangeTotal: await changes.CountAsync(token),
            ApprovalPending: await changes.CountAsync(t => t.ApprovalStatus == ApprovalStatus.Pending, token),
            ApprovalApproved: await changes.CountAsync(t => t.ApprovalStatus == ApprovalStatus.Approved, token),
            ApprovalRejected: await changes.CountAsync(t => t.ApprovalStatus == ApprovalStatus.Rejected, token),
            ApprovalDecisions: await kararlar.CountAsync(token),
            DelegatedDecisions: await kararlar.CountAsync(a => a.OnBehalfOfId != null, token),
            // Tur > 1: reddedilip düzeltilerek yeniden gönderilmiş talepler
            ResubmittedChanges: await changes.CountAsync(t => t.ApprovalRound > 1, token),
            AvgApprovalHours: kararSureleri.Count == 0
                ? null
                : Math.Round(kararSureleri.Average(), 1));
    }
}
