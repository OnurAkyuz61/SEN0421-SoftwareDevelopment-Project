using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

public class EscalationReportFilter
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? Level { get; set; }
    public bool? Manual { get; set; }
    public int? TeamId { get; set; }
    public int Page { get; set; } = 1;
}

public sealed record EscalationSummary(
    int Total, int Manual, int Automatic, int Fallback, int PriorityRaised,
    double AvgMinutesOverdue, int DistinctTickets,
    List<(int Level, int Count)> ByLevel,
    List<(string Name, int Count)> ByTarget,
    List<(string Team, int Count)> ByTeam);

// Kullanılmayan makale raporunun eşikleri. Varsayılanlar KnowledgeUsagePolicy'de;
// kurum "kaç gün eski sayılır" kararını sorgu dizesinden değiştirebilir.
public class KnowledgeUsageFilter
{
    public int StaleDays { get; set; } = KnowledgeUsagePolicy.DefaultStaleDays;
    public int MinVotes { get; set; } = KnowledgeUsagePolicy.DefaultMinVotes;
    public int NotHelpfulPercent { get; set; } = KnowledgeUsagePolicy.DefaultNotHelpfulPercent;

    // false: yayındaki bütün makaleler listelenir (karşılaştırma için)
    public bool OnlyIssues { get; set; } = true;

    // Dışarıdan gelen değer sınırlanır: 0 gün verilse her makale "eski"
    // görünür, 1000 gün verilse rapor hiç dolmaz
    public void Normalize()
    {
        StaleDays = Math.Clamp(StaleDays, 1, 3650);
        MinVotes = Math.Clamp(MinVotes, 1, 1000);
        NotHelpfulPercent = Math.Clamp(NotHelpfulPercent, 1, 100);
    }
}

public sealed record KnowledgeUsageRow(
    int Id, string Title, string? Category, int UsedCount, DateTime? LastUsedAt,
    DateTime CreatedAt, int HelpfulCount, int NotHelpfulCount, KnowledgeUsageIssue Issues)
{
    public int TotalVotes => KnowledgeUsagePolicy.TotalVotes(HelpfulCount, NotHelpfulCount);
    public int? NotHelpfulPercent => KnowledgeUsagePolicy.NotHelpfulPercent(HelpfulCount, NotHelpfulCount);
    public IReadOnlyList<string> Reasons => KnowledgeUsagePolicy.Describe(Issues);
}

public sealed record KnowledgeUsageReport(
    List<KnowledgeUsageRow> Rows,
    int PublishedTotal, int NeverUsed, int Stale, int Negative, int NoVotes,
    List<ChatbotQuestion> Questions, int QuestionTotal, int QuestionAskTotal);

// Kaynak bazlı uygunsuzluk raporunun süzgeci. Dönem DetectedAt'e göre:
// "bu ayda kaç uygunsuzluk DOĞDU" sorusu, kapanışa göre süzmekten farklıdır.
public class NonconformityReportFilter
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public NonconformitySource? Source { get; set; }
    public NonconformityStatus? Status { get; set; }

    // true: yalnızca termini geçmiş açık kayıtlar (takip listesi)
    public bool OverdueOnly { get; set; }

    public int Page { get; set; } = 1;
}

// Yönetim gözden geçirme paketinin dönemi. Dönem yerel gün olarak seçilir;
// karşılaştırma dönemi hemen öncesindeki eşit uzunlukta penceredir.
public class ManagementReviewFilter
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    // Varsayılan: içinde bulunduğumuz ayla birlikte son 3 ay (çeyrek).
    // Yönetim gözden geçirmesi günlük bir ekran değil; dönemi buna göre geniş.
    public ManagementReviewPeriod Normalize()
    {
        var today = AppTime.LocalToday;
        var to = (To ?? today).Date;
        var from = (From ?? new DateTime(today.Year, today.Month, 1).AddMonths(-2)).Date;

        // Ters verilen aralık sessizce boş rapor üretmesin
        if (from > to) (from, to) = (to, from);

        From = from;
        To = to;
        return new ManagementReviewPeriod(from, to);
    }
}

// Eskalasyon geçmişi raporu (ISO 9001 md. 9.1.3: analiz ve değerlendirme).
// Hangi seviyede ne kadar eskalasyon oldu, ne kadarı elle tetiklendi,
// kaçında yedek kurala düşüldü, hangi ekip ve kişilere yığıldı.
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class ReportsController : Controller
{
    private readonly AppDbContext _db;
    private readonly ManagementReviewService _review;

    public ReportsController(AppDbContext db, ManagementReviewService review)
    {
        _db = db;
        _review = review;
    }

    private IQueryable<EscalationRecord> Query(EscalationReportFilter f)
    {
        // Varsayılan dönem: son 30 gün
        var fromLocal = (f.From ?? AppTime.LocalToday.AddDays(-30)).Date;
        var toLocal = (f.To ?? AppTime.LocalToday).Date;
        f.From = fromLocal;
        f.To = toLocal;
        var fromUtc = AppTime.ToUtc(fromLocal);
        var toUtc = AppTime.ToUtc(toLocal.AddDays(1));

        var q = _db.EscalationRecords.AsNoTracking()
            .Where(e => e.OccurredAt >= fromUtc && e.OccurredAt < toUtc);

        if (f.Level != null) q = q.Where(e => e.Level == f.Level);
        if (f.Manual != null) q = q.Where(e => e.IsManual == f.Manual);
        if (f.TeamId != null) q = q.Where(e => e.Ticket!.AssignedTeamId == f.TeamId);
        return q;
    }

    public async Task<IActionResult> Escalations([FromQuery] EscalationReportFilter f)
    {
        var q = Query(f);

        var total = await q.CountAsync();
        var byLevel = (await q.GroupBy(e => e.Level).Select(g => new { g.Key, C = g.Count() }).OrderBy(x => x.Key).ToListAsync())
            .Select(x => (x.Key, x.C)).ToList();
        var byTarget = (await q.Where(e => e.EscalatedToId != null)
                .GroupBy(e => new { e.EscalatedTo!.FirstName, e.EscalatedTo.LastName })
                .Select(g => new { g.Key.FirstName, g.Key.LastName, C = g.Count() })
                .OrderByDescending(x => x.C).Take(10).ToListAsync())
            .Select(x => ($"{x.FirstName} {x.LastName}", x.C)).ToList();
        var byTeam = (await q.GroupBy(e => e.Ticket!.AssignedTeam != null ? e.Ticket.AssignedTeam.Name : "Ekipsiz")
                .Select(g => new { g.Key, C = g.Count() }).OrderByDescending(x => x.C).Take(10).ToListAsync())
            .Select(x => (x.Key, x.C)).ToList();

        var summary = new EscalationSummary(
            total,
            await q.CountAsync(e => e.IsManual),
            await q.CountAsync(e => !e.IsManual),
            await q.CountAsync(e => e.UsedFallback),
            await q.CountAsync(e => e.PriorityAfter != null),
            total == 0 ? 0 : await q.AverageAsync(e => (double)e.MinutesOverdue),
            await q.Select(e => e.TicketId).Distinct().CountAsync(),
            byLevel, byTarget, byTeam);

        var (rows, pager) = await q
            .Include(e => e.Ticket)
            .Include(e => e.EscalatedTo)
            .Include(e => e.TriggeredBy)
            .OrderByDescending(e => e.OccurredAt)
            .ToPageAsync(f.Page);

        ViewBag.Filter = f;
        ViewBag.Summary = summary;
        ViewBag.Pager = pager;
        ViewBag.Teams = await _db.Teams.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();
        return View(rows);
    }

    // Aynı filtreyle CSV dışa aktarma (Excel Türkçe karakterleri doğru açsın diye UTF-8 BOM)
    public async Task<IActionResult> EscalationsCsv([FromQuery] EscalationReportFilter f)
    {
        var rows = await Query(f)
            .Include(e => e.Ticket).ThenInclude(t => t!.AssignedTeam)
            .Include(e => e.EscalatedTo)
            .Include(e => e.TriggeredBy)
            .OrderByDescending(e => e.OccurredAt)
            .Take(10_000)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Tarih;Talep No;Başlık;Ekip;Seviye;Tür;Gecikme (dk);İlgilenen;Tetikleyen;Yedek;Öncelik Önce;Öncelik Sonra;Not");
        foreach (var e in rows)
        {
            sb.AppendLine(string.Join(';', new[]
            {
                AppTime.ToLocal(e.OccurredAt).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
                e.Ticket == null ? $"#{e.TicketId}" : Ui.TicketNo(e.Ticket),
                e.Ticket?.Title ?? "",
                e.Ticket?.AssignedTeam?.Name ?? "",
                e.Level.ToString(CultureInfo.InvariantCulture),
                e.IsManual ? "Manuel" : "Otomatik",
                e.MinutesOverdue.ToString(CultureInfo.InvariantCulture),
                e.EscalatedTo?.FullName ?? "",
                e.TriggeredBy?.FullName ?? "",
                e.UsedFallback ? "Evet" : "Hayır",
                e.PriorityBefore == null ? "" : Ui.PriorityText(e.PriorityBefore.Value),
                e.PriorityAfter == null ? "" : Ui.PriorityText(e.PriorityAfter.Value),
                e.Note ?? ""
            }.Select(Csv)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"eskalasyon-{f.From:yyyyMMdd}-{f.To:yyyyMMdd}.csv");
    }

    // ───── Kullanılmayan makale raporu ───────────────────────────────────
    //
    // "Hangi makale işe yaramıyor" ve "hangi makale eksik" sorularını yan
    // yana cevaplar (md. 9.1.3 / 10.3):
    //   • hiç kullanılmamış, son kullanımı eski, olumsuz oy almış makaleler
    //   • chatbot'un cevaplayamadığı sorular — yazılması gereken makaleler
    //
    // Sınıflandırma SQL'de değil bellekte yapılıyor: eşik ve yuvarlama
    // kuralı tek yerde (KnowledgeUsagePolicy) kalsın. Aynı kural bir de
    // SQL'e yazılsaydı yüzde yuvarlamasının sınırında iki taraf farklı
    // karar verir, makale listeye girer ama rozeti görünmezdi. Bilgi
    // bankası yüz binlerce satırlık bir tablo değil; yayındaki makaleleri
    // küçük bir projeksiyonla okumak bu rapor için yeterli.
    public async Task<IActionResult> KnowledgeUsage([FromQuery] KnowledgeUsageFilter f)
    {
        f.Normalize();
        var now = AppTime.UtcNow;

        var yayinda = await _db.KnowledgeArticles.AsNoTracking()
            .Where(a => a.Status == KnowledgeArticleStatus.Published)
            .Select(a => new
            {
                a.Id, a.Title, Kategori = a.Category == null ? null : a.Category.Name,
                a.UsedCount, a.LastUsedAt, a.CreatedAt, a.HelpfulCount, a.NotHelpfulCount
            })
            .ToListAsync();

        var tumu = yayinda
            .Select(a => new KnowledgeUsageRow(
                a.Id, a.Title, a.Kategori, a.UsedCount, a.LastUsedAt, a.CreatedAt,
                a.HelpfulCount, a.NotHelpfulCount,
                KnowledgeUsagePolicy.Classify(a.UsedCount, a.CreatedAt, a.LastUsedAt,
                    a.HelpfulCount, a.NotHelpfulCount, now,
                    f.StaleDays, f.MinVotes, f.NotHelpfulPercent)))
            .ToList();

        var rows = (f.OnlyIssues ? tumu.Where(r => r.Issues != KnowledgeUsageIssue.None) : tumu)
            // Olumsuz geri bildirim en önce: okuyucu doğrudan "bu makale
            // işime yaramadı" demiş, en acil gözden geçirilecek olan bu.
            .OrderBy(r => r.Issues.HasFlag(KnowledgeUsageIssue.NegativeFeedback) ? 0 : 1)
            .ThenBy(r => r.UsedCount)
            .ThenBy(r => r.LastUsedAt ?? r.CreatedAt)
            .ToList();

        // Yazılması gereken makaleler: en çok sorulan cevapsız sorular önce
        var sorular = await _db.ChatbotQuestions.AsNoTracking()
            .Where(q => !q.IsResolved)
            .OrderByDescending(q => q.AskCount).ThenByDescending(q => q.LastAskedAt)
            .Take(20)
            .ToListAsync();

        var cevapsiz = _db.ChatbotQuestions.Where(q => !q.IsResolved);

        var report = new KnowledgeUsageReport(
            rows,
            PublishedTotal: tumu.Count,
            NeverUsed: tumu.Count(r => r.Issues.HasFlag(KnowledgeUsageIssue.NeverUsed)),
            Stale: tumu.Count(r => r.Issues.HasFlag(KnowledgeUsageIssue.Stale)),
            Negative: tumu.Count(r => r.Issues.HasFlag(KnowledgeUsageIssue.NegativeFeedback)),
            NoVotes: tumu.Count(r => r.TotalVotes == 0),
            Questions: sorular,
            QuestionTotal: await cevapsiz.CountAsync(),
            QuestionAskTotal: await cevapsiz.SumAsync(q => q.AskCount));

        ViewBag.Filter = f;
        return View(report);
    }

    // ───── Kaynak bazlı uygunsuzluk raporu ──────────────────────────────
    //
    // "Uygunsuzluklarımız nereden doğuyor ve gerçekten kapanıyor mu"
    // (md. 9.1.3 / 10.2): SLA ihlali, düşük memnuniyet, iç denetim ve elle
    // açılan kayıtların kırılımı; ortalama kapanış süresi, termin uyumu ve
    // açık / kapalı dağılımı.
    //
    // Kırılımın hesabı NonconformityReportPolicy'de (saf sınıf): "kapanış
    // süresi" ve "termin uyumu" tanımı tek yerde kalsın.
    private IQueryable<Nonconformity> Query(NonconformityReportFilter f)
    {
        // Varsayılan dönem: son 90 gün. Uygunsuzluk talepten seyrek bir
        // kayıttır; 30 günlük pencere çoğu kurumda boş görünür.
        var fromLocal = (f.From ?? AppTime.LocalToday.AddDays(-90)).Date;
        var toLocal = (f.To ?? AppTime.LocalToday).Date;
        f.From = fromLocal;
        f.To = toLocal;
        var fromUtc = AppTime.ToUtc(fromLocal);
        var toUtc = AppTime.ToUtc(toLocal.AddDays(1));

        var q = _db.Nonconformities.AsNoTracking()
            .Where(n => n.DetectedAt >= fromUtc && n.DetectedAt < toUtc);

        if (f.Source != null) q = q.Where(n => n.Source == f.Source);
        if (f.Status != null) q = q.Where(n => n.Status == f.Status);
        if (f.OverdueOnly)
        {
            var now = AppTime.UtcNow;
            q = q.Where(n => n.Status != NonconformityStatus.Closed && n.DueAt != null && n.DueAt < now);
        }
        return q;
    }

    private static IQueryable<NonconformityReportRow> Rows(IQueryable<Nonconformity> q) =>
        q.Select(n => new NonconformityReportRow(
            n.Id, n.Source, n.Status, n.DetectedAt, n.DueAt, n.ClosedAt,
            n.EffectivenessVerified == true && n.VerifiedById != null,
            n.Attachments.Count,
            n.Responsible == null ? null : n.Responsible.FirstName + " " + n.Responsible.LastName,
            n.TicketId,
            n.Ticket == null ? null : n.Ticket.Number,
            n.Description));

    public async Task<IActionResult> Nonconformities([FromQuery] NonconformityReportFilter f)
    {
        var q = Query(f);
        var now = AppTime.UtcNow;

        // Kırılım TÜM dönem üzerinden (sayfalama yalnızca listeyi böler)
        var all = await Rows(q).ToListAsync();

        ViewBag.Filter = f;
        ViewBag.Stats = NonconformityReportPolicy.Summarize(all, now);
        ViewBag.Now = now;

        // Sıralama projeksiyondan ÖNCE: açık kayıtlar üstte, termini yakın olan
        // önce. Projeksiyonun üstüne OrderBy yazılsa EF alanı geri eşlemek
        // zorunda kalır; kaynakta sıralamak hem çevrilebilir hem okunur.
        var (rows, pager) = await Rows(q
                .OrderBy(n => n.Status)
                .ThenBy(n => n.DueAt ?? n.DetectedAt))
            .ToPageAsync(f.Page);

        ViewBag.Pager = pager;
        return View(rows);
    }

    // Aynı filtreyle CSV: raporun konusu kaynak kırılımı olduğu için satırlar
    // kaynak başına özet + TOPLAM. (Excel Türkçe karakterleri doğru açsın
    // diye UTF-8 BOM.)
    public async Task<IActionResult> NonconformitiesCsv([FromQuery] NonconformityReportFilter f)
    {
        var all = await Rows(Query(f)).Take(50_000).ToListAsync();
        var stats = NonconformityReportPolicy.Summarize(all, AppTime.UtcNow);

        var sb = new StringBuilder();
        sb.AppendLine("Kaynak;Toplam;Açık;İşlemde;Kapalı;Kapanma Oranı (%);" +
                      "Ort. Kapanış (gün);Zamanında Kapanan;Geciken;Termin Uyumu (%);" +
                      "Termini Geçen Açık;Etkinliği Doğrulanan");
        foreach (var s in stats)
        {
            sb.AppendLine(string.Join(';', new[]
            {
                s.Source == null ? "TOPLAM" : Ui.NonconformitySourceText(s.Source.Value),
                s.Total.ToString(CultureInfo.InvariantCulture),
                s.Open.ToString(CultureInfo.InvariantCulture),
                s.InProgress.ToString(CultureInfo.InvariantCulture),
                s.Closed.ToString(CultureInfo.InvariantCulture),
                s.ClosedRate.ToString("0.0", CultureInfo.InvariantCulture),
                s.AvgClosureDays == null ? "" : s.AvgClosureDays.Value.ToString("0.0", CultureInfo.InvariantCulture),
                s.ClosedOnTime.ToString(CultureInfo.InvariantCulture),
                s.ClosedLate.ToString(CultureInfo.InvariantCulture),
                s.OnTimeRate == null ? "" : s.OnTimeRate.Value.ToString("0.0", CultureInfo.InvariantCulture),
                s.OverdueOpen.ToString(CultureInfo.InvariantCulture),
                s.Verified.ToString(CultureInfo.InvariantCulture)
            }.Select(Csv)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"uygunsuzluk-{f.From:yyyyMMdd}-{f.To:yyyyMMdd}.csv");
    }

    // ───── Yönetim gözden geçirme paketi (md. 9.3) ──────────────────────
    //
    // ISO 9001 md. 9.3.2 gözden geçirmenin belirli girdileri kapsamasını
    // ister: süreç performansı ve göstergeler, müşteri memnuniyeti,
    // uygunsuzluklar ve düzeltici faaliyetler, kaynakların yeterliliği.
    // Bu girdiler sistemde ayrı ekranlarda duruyordu; paket onları tek
    // dönemsel sayfada birleştirir.
    //
    // YALNIZCA OKUMA: hiçbir kayıt üretilmez, şema değişmez. Hesap
    // ManagementReviewService + ManagementReviewPolicy'de; ekran ve CSV aynı
    // kaynaktan beslenir, ikisi birbirinden ayrı düşemez.
    public async Task<IActionResult> ManagementReview([FromQuery] ManagementReviewFilter f,
                                                      CancellationToken token)
    {
        var report = await _review.BuildAsync(f.Normalize(), token);

        ViewBag.Filter = f;
        // Zayıf boyut eşiği uygunsuzluk eşiğiyle aynı (Quality bölümü)
        ViewBag.LowThreshold = _review.LowSatisfactionThreshold;
        return View(report);
    }

    // Aynı dönemin CSV'si. Rapor karışık yapıda olduğu için uzun biçim:
    // Bölüm;Gösterge;Değer;Önceki Dönem;Değişim (Excel Türkçe karakterleri
    // doğru açsın diye UTF-8 BOM).
    public async Task<IActionResult> ManagementReviewCsv([FromQuery] ManagementReviewFilter f,
                                                         CancellationToken token)
    {
        var period = f.Normalize();
        var report = await _review.BuildAsync(period, token);

        var sb = new StringBuilder();
        sb.AppendLine("Bölüm;Gösterge;Değer;Önceki Dönem;Değişim");
        foreach (var row in ManagementReviewPolicy.CsvRows(report))
            sb.AppendLine(string.Join(';', new[] { row.Section, row.Metric, row.Value, row.Previous, row.Change }
                .Select(Csv)));

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8",
            $"yonetim-gozden-gecirme-{period.FromLocal:yyyyMMdd}-{period.ToLocal:yyyyMMdd}.csv");
    }

    // CSV hücresi: ayırıcı / tırnak / satır sonu kaçışı ve formül enjeksiyonu engeli.
    //
    // Tırnak işareti yalnızca GERÇEKTEN formüle dönüşebilecek değere eklenir:
    // tamamı sayı olan bir hücre (ör. dönem karşılaştırmasındaki "-33.3")
    // formül değildir ve tırnaklanırsa Excel'de metin olur — sayı sütununda
    // toplama, sıralama, grafik çalışmaz. "-1+1+cmd|..." gibi sayı olarak
    // ayrıştırılamayan değerler ise korumayı almaya devam eder.
    private static string Csv(string v)
    {
        if (v.Length > 0 && "=+-@".Contains(v[0])
            && !double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            v = "'" + v;

        return v.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}

// Yönetici: güvenlik ve süreç denetim kaydı + OTP gönderim kayıtları
[AuthorizeRole(UserRole.Admin)]
public class AuditController : Controller
{
    private readonly AppDbContext _db;
    public AuditController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(AuditAction? action, string? q, int page = 1)
    {
        var query = _db.AuditLogs.AsNoTracking().Include(a => a.User).AsQueryable();
        if (action != null) query = query.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(a => (a.Email != null && a.Email.Contains(s)) || (a.Detail != null && a.Detail.Contains(s))
                                     || (a.IpAddress != null && a.IpAddress.Contains(s)));
        }

        var (items, pager) = await query.OrderByDescending(a => a.CreatedAt).ToPageAsync(page);
        ViewBag.Action = action;
        ViewBag.Q = q;
        ViewBag.Pager = pager;
        return View(items);
    }

    public async Task<IActionResult> OtpDeliveries(OtpDeliveryChannel? channel, OtpDeliveryStatus? status, int page = 1)
    {
        var query = _db.OtpDeliveryLogs.AsNoTracking().Include(o => o.User).AsQueryable();
        if (channel != null) query = query.Where(o => o.Channel == channel);
        if (status != null) query = query.Where(o => o.Status == status);

        var (items, pager) = await query.OrderByDescending(o => o.CreatedAt).ToPageAsync(page);

        // Kanal bazında teslim oranı (son 7 gün)
        var since = AppTime.UtcNow.AddDays(-7);
        ViewBag.Stats = await _db.OtpDeliveryLogs.AsNoTracking()
            .Where(o => o.CreatedAt >= since)
            .GroupBy(o => o.Channel)
            .Select(g => new OtpChannelStat(g.Key, g.Count(),
                g.Count(o => o.Status == OtpDeliveryStatus.Delivered || o.Status == OtpDeliveryStatus.Read),
                g.Count(o => o.Status == OtpDeliveryStatus.Failed)))
            .ToListAsync();

        ViewBag.Channel = channel;
        ViewBag.Status = status;
        ViewBag.Pager = pager;
        return View(items);
    }
}

public sealed record OtpChannelStat(OtpDeliveryChannel Channel, int Total, int Delivered, int Failed)
{
    public double DeliveryRate => Total == 0 ? 0 : 100.0 * Delivered / Total;
}
