using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// YÖNETİM PANELİ: ISO 9001 süreç performans göstergeleri + bekleyen işler
//
// Göstergeler SQL tarafında hesaplanır. Önceden tüm Tickets tablosu belleğe
// çekilip LINQ ile sayılıyordu; tablo büyüdükçe sayfa yavaşlıyordu.
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class DashboardController : Controller
{
    private const int ListSize = 5;      // yan panellerde gösterilen kayıt sayısı
    private const int TrendDays = 14;    // günlük grafik penceresi

    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var tickets = _db.Tickets.AsNoTracking();

        // Açık talep: henüz çözülmemiş
        var acik = tickets.Where(t => t.Status == TicketStatus.New || t.Status == TicketStatus.InProgress);

        var toplam = await tickets.CountAsync();
        var ihlalToplam = await tickets.CountAsync(t => t.IsSlaBreached);

        // Çözüm ve ilk yanıt süreleri: ortalama SQL tarafında alınır
        var cozulmusSureler = await tickets
            .Where(t => t.ResolvedAt != null)
            .Select(t => EF.Functions.DateDiffMinute(t.CreatedAt, t.ResolvedAt!.Value))
            .ToListAsync();

        var puanlar = await _db.SurveyAnswers.AsNoTracking().Select(a => a.Score).ToListAsync();

        // Durum ve kategori kırılımları: tek GROUP BY
        var durumSayilari = await tickets
            .GroupBy(t => t.Status)
            .Select(g => new { Durum = g.Key, Adet = g.Count() })
            .ToDictionaryAsync(x => x.Durum, x => x.Adet);

        var kategoriSayilari = await tickets
            .GroupBy(t => t.CategoryId)
            .Select(g => new { Id = g.Key, Adet = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Adet);

        var kategoriler = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();

        // Günlük grafik: yalnızca pencereye düşen iki tarih kolonu çekilir,
        // yerel güne çevirme bellekte yapılır (UTC->yerel SQL'e çevrilemiyor)
        var bugun = AppTime.LocalToday;
        var gunler = Enumerable.Range(0, TrendDays).Select(i => bugun.AddDays(i - (TrendDays - 1))).ToList();
        var pencereBaslangic = AppTime.ToUtc(gunler[0]);

        var tarihler = await tickets
            .Where(t => t.CreatedAt >= pencereBaslangic || (t.ResolvedAt != null && t.ResolvedAt >= pencereBaslangic))
            .Select(t => new { t.CreatedAt, t.ResolvedAt })
            .ToListAsync();

        var acilanGunler = tarihler
            .GroupBy(t => AppTime.ToLocal(t.CreatedAt).Date)
            .ToDictionary(g => g.Key, g => g.Count());
        var cozulenGunler = tarihler
            .Where(t => t.ResolvedAt != null)
            .GroupBy(t => AppTime.ToLocal(t.ResolvedAt!.Value).Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var simdi = AppTime.UtcNow;

        var vm = new DashboardVm
        {
            Total = toplam,
            Open = await acik.CountAsync(),
            Unassigned = await acik.CountAsync(t => t.AssigneeId == null),
            PendingApproval = await tickets.CountAsync(t => t.ApprovalStatus == ApprovalStatus.Pending),
            Breached = await acik.CountAsync(t => t.IsSlaBreached),
            SlaRate = toplam == 0 ? 100 : Math.Round(100.0 * (toplam - ihlalToplam) / toplam, 1),
            Mttr = cozulmusSureler.Count == 0 ? 0 : Math.Round(cozulmusSureler.Average() / 60.0, 1),
            Csat = puanlar.Count == 0 ? 0 : Math.Round(puanlar.Average(), 2),

            StatusLabels = Enum.GetValues<TicketStatus>().Select(Ui.StatusText).ToArray(),
            StatusCounts = Enum.GetValues<TicketStatus>()
                .Select(s => durumSayilari.TryGetValue(s, out var n) ? n : 0).ToArray(),
            CategoryLabels = kategoriler.Select(c => c.Name).ToArray(),
            CategoryCounts = kategoriler
                .Select(c => kategoriSayilari.TryGetValue(c.Id, out var n) ? n : 0).ToArray(),
            DayLabels = gunler.Select(d => d.ToString("dd.MM")).ToArray(),
            DayCreated = gunler.Select(d => acilanGunler.TryGetValue(d, out var n) ? n : 0).ToArray(),
            DayResolved = gunler.Select(d => cozulenGunler.TryGetValue(d, out var n) ? n : 0).ToArray(),

            // Yan paneller: yalnızca ilk beş kayıt çekilir
            Approvals = await tickets
                .Where(t => t.ApprovalStatus == ApprovalStatus.Pending)
                .OrderBy(t => t.CreatedAt).Take(ListSize).ToListAsync(),
            UnassignedList = await acik
                .Where(t => t.AssigneeId == null)
                .Include(t => t.Category)
                .OrderBy(t => t.SlaDueAt).Take(ListSize).ToListAsync(),
            BreachedList = await acik
                .Where(t => t.IsSlaBreached)
                .Include(t => t.Assignee)
                .OrderBy(t => t.SlaDueAt).Take(ListSize).ToListAsync(),
            LatestSurveys = await _db.SurveyResponses.AsNoTracking()
                .Include(r => r.Ticket).Include(r => r.Answers)
                .OrderByDescending(r => r.CreatedAt).Take(ListSize).ToListAsync(),

            MonthlyKpi = await _db.KpiSnapshots.AsNoTracking()
                .Where(k => k.Period == KpiPeriod.Monthly && k.DepartmentId == null)
                .OrderByDescending(k => k.PeriodStart).Take(6).ToListAsync(),
            OpenNonconformities = await _db.Nonconformities
                .CountAsync(n => n.Status != NonconformityStatus.Closed),
            OverdueNonconformities = await _db.Nonconformities
                .CountAsync(n => n.Status != NonconformityStatus.Closed && n.DueAt != null && n.DueAt < simdi)
        };

        return View(vm);
    }
}
