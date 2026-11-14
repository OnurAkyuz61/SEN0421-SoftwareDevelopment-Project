using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Bir dönemin göstergelerini hesaplar ve KpiSnapshot olarak dondurur (md. 9.1.1).
// Panel artık her açılışta sıfırdan hesaplamak yerine bu kayıtlardan
// dönem karşılaştırması yapabiliyor.
public class KpiService
{
    private readonly AppDbContext _db;
    // "Düşük puanlı yanıt" sayımı uygunsuzluk eşiğiyle AYNI eşiği kullanır:
    // panelde görünen sayı ile açılan kalite kaydı sayısı birbirini tutsun
    // (appsettings.json → "Quality").
    private readonly QualitySettings _quality;

    public KpiService(AppDbContext db, IOptions<QualitySettings> quality)
    {
        _db = db;
        _quality = quality.Value;
    }

    // Verilen günün göstergelerini hesaplar; kayıt varsa günceller
    public Task<KpiSnapshot> UpsertDailyAsync(DateTime localDay, int? departmentId = null)
    {
        var start = AppTime.ToUtc(localDay.Date);
        var end = AppTime.ToUtc(localDay.Date.AddDays(1));
        return UpsertAsync(KpiPeriod.Daily, start, end, departmentId);
    }

    public Task<KpiSnapshot> UpsertMonthlyAsync(int year, int month, int? departmentId = null)
    {
        var start = AppTime.ToUtc(new DateTime(year, month, 1));
        var end = AppTime.ToUtc(new DateTime(year, month, 1).AddMonths(1));
        return UpsertAsync(KpiPeriod.Monthly, start, end, departmentId);
    }

    private async Task<KpiSnapshot> UpsertAsync(KpiPeriod period, DateTime startUtc, DateTime endUtc, int? departmentId)
    {
        // Dönemde AÇILAN talepler üzerinden ölçülür
        var q = _db.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= startUtc && t.CreatedAt < endUtc);

        if (departmentId != null)
            q = q.Where(t => t.Requester!.DepartmentId == departmentId);

        var tickets = await q
            .Select(t => new
            {
                t.Id,
                t.CreatedAt,
                t.ResolvedAt,
                t.FirstRespondedAt,
                t.IsSlaBreached,
                t.ReopenCount,
                t.FirstResponseDueAt,
                t.IsFirstResponseBreached
            })
            .ToListAsync();

        var resolved = tickets.Where(t => t.ResolvedAt != null).ToList();
        var responded = tickets.Where(t => t.FirstRespondedAt != null).ToList();

        // ── Memnuniyet (CSAT) ──────────────────────────────────────────
        // Ortalamanın yanında ölçümün kendi kalitesi de kaydedilir: kaç yanıt
        // geldi, çözülen taleplerin kaçı değerlendirildi, kaç yanıt eşik
        // altında. Dört değer birlikte okunur — iki yanıttan gelen 4,8
        // ortalama dönemi temsil etmez (ISO 9001 md. 9.1.2).
        var ids = tickets.Select(t => t.Id).ToList();
        var csat = 0m;
        var responseCount = 0;
        var lowCount = 0;

        if (ids.Count > 0)
        {
            // Yanıt bazında grupla: ortalama cevaplardan, "düşük puanlı yanıt"
            // sayısı ise yanıtın kendi ortalamasından hesaplanır
            var answers = await _db.SurveyAnswers.AsNoTracking()
                .Where(a => ids.Contains(a.Response!.TicketId))
                .Select(a => new { a.ResponseId, a.Score })
                .ToListAsync();

            if (answers.Count > 0)
            {
                csat = Math.Round((decimal)answers.Average(a => a.Score), 2);

                var perResponse = answers
                    .GroupBy(a => a.ResponseId)
                    .Select(g => g.Average(a => a.Score))
                    .ToList();

                responseCount = perResponse.Count;
                lowCount = perResponse.Count(avg => avg < _quality.LowSatisfactionThreshold);
            }
        }

        var snap = await _db.KpiSnapshots.FirstOrDefaultAsync(k =>
            k.Period == period && k.PeriodStart == startUtc && k.DepartmentId == departmentId);

        if (snap == null)
        {
            snap = new KpiSnapshot { Period = period, PeriodStart = startUtc, DepartmentId = departmentId };
            _db.KpiSnapshots.Add(snap);
        }

        snap.PeriodEnd = endUtc;
        snap.TotalTickets = tickets.Count;
        snap.ResolvedTickets = resolved.Count;
        snap.BreachedTickets = tickets.Count(t => t.IsSlaBreached);
        snap.ReopenCount = tickets.Sum(t => t.ReopenCount);
        // İlk yanıt SLA'sı ayrı gösterge: yalnızca ilk yanıt hedefi olan talepler sayılır
        var frMeasured = tickets.Where(t => t.FirstResponseDueAt != null).ToList();
        snap.FirstResponseBreachedTickets = frMeasured.Count(t => t.IsFirstResponseBreached);
        snap.FirstResponseComplianceRate = frMeasured.Count == 0
            ? 100m
            : Math.Round(100m * (frMeasured.Count - snap.FirstResponseBreachedTickets) / frMeasured.Count, 1);
        snap.SlaComplianceRate = tickets.Count == 0
            ? 100m
            : Math.Round(100m * (tickets.Count - snap.BreachedTickets) / tickets.Count, 1);
        snap.AvgResolutionHours = resolved.Count == 0
            ? 0m
            : Math.Round((decimal)resolved.Average(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalHours), 1);
        snap.AvgFirstResponseHours = responded.Count == 0
            ? 0m
            : Math.Round((decimal)responded.Average(t => (t.FirstRespondedAt!.Value - t.CreatedAt).TotalHours), 1);
        snap.CsatAverage = csat;
        snap.SurveyResponseCount = responseCount;
        snap.LowCsatCount = lowCount;
        // Yanıt oranının paydası ÇÖZÜLEN talep sayısı: anket yalnızca çözülen
        // talep için sorulur, açık talepleri paydaya koymak oranı gerçekte
        // olduğundan düşük gösterirdi. Çözülen talep yoksa oran 0 kalır.
        //
        // %100'de sınırlanır: değerlendirildikten sonra yeniden açılan talep
        // yanıtı korur ama ResolvedAt'i boşalır (TicketWorkflow.Apply), yani
        // pay paydadan büyük çıkabilir. Panelde "%150 yanıt oranı" görmek
        // göstergeye olan güveni bitirir.
        snap.CsatResponseRate = resolved.Count == 0
            ? 0m
            : Math.Min(100m, Math.Round(100m * responseCount / resolved.Count, 1));
        snap.CalculatedAt = AppTime.UtcNow;

        await _db.SaveChangesAsync();
        return snap;
    }
}

// Göstergeleri düzenli olarak dondurur. Gün içinde de çalışır ki panel
// güncel kalsın; dönem kapandığında son hesap kalıcı kayıt olur.
public class KpiAggregatorService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<KpiAggregatorService> _log;

    public KpiAggregatorService(IServiceScopeFactory scopes, ILogger<KpiAggregatorService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var kpi = scope.ServiceProvider.GetRequiredService<KpiService>();

                var today = AppTime.LocalToday;
                await kpi.UpsertDailyAsync(today);
                await kpi.UpsertDailyAsync(today.AddDays(-1));
                await kpi.UpsertMonthlyAsync(today.Year, today.Month);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _log.LogError(ex, "KPI hesaplaması başarısız oldu"); }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }
}
