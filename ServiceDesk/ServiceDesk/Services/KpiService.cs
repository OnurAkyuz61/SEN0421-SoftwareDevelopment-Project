using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Bir dönemin göstergelerini hesaplar ve KpiSnapshot olarak dondurur (md. 9.1.1).
// Panel artık her açılışta sıfırdan hesaplamak yerine bu kayıtlardan
// dönem karşılaştırması yapabiliyor.
public class KpiService
{
    private readonly AppDbContext _db;
    public KpiService(AppDbContext db) => _db = db;

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
                t.ReopenCount
            })
            .ToListAsync();

        var resolved = tickets.Where(t => t.ResolvedAt != null).ToList();
        var responded = tickets.Where(t => t.FirstRespondedAt != null).ToList();

        var ids = tickets.Select(t => t.Id).ToList();
        var csat = 0m;
        if (ids.Count > 0)
        {
            var scores = await _db.SurveyAnswers.AsNoTracking()
                .Where(a => ids.Contains(a.Response!.TicketId))
                .Select(a => a.Score)
                .ToListAsync();
            if (scores.Count > 0) csat = Math.Round((decimal)scores.Average(), 2);
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
