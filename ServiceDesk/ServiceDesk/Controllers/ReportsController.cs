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

// Eskalasyon geçmişi raporu (ISO 9001 md. 9.1.3: analiz ve değerlendirme).
// Hangi seviyede ne kadar eskalasyon oldu, ne kadarı elle tetiklendi,
// kaçında yedek kurala düşüldü, hangi ekip ve kişilere yığıldı.
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class ReportsController : Controller
{
    private readonly AppDbContext _db;
    public ReportsController(AppDbContext db) => _db = db;

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

    // CSV hücresi: ayırıcı / tırnak / satır sonu kaçışı ve formül enjeksiyonu engeli
    private static string Csv(string v)
    {
        if (v.Length > 0 && "=+-@".Contains(v[0])) v = "'" + v;
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
