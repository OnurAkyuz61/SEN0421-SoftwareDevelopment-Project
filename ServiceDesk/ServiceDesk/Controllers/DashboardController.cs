using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Controllers;

// KPI paneli (ISO 9001 süreç performans göstergeleri)
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var tickets = await _db.Tickets.ToListAsync();
        var scores = await _db.Feedbacks.Select(f => f.Score).ToListAsync();
        var resolved = tickets.Where(t => t.ResolvedAt != null).ToList();

        ViewBag.Total = tickets.Count;
        ViewBag.Open = tickets.Count(t => t.Status is TicketStatus.New or TicketStatus.InProgress);
        ViewBag.Breached = tickets.Count(t => t.IsSlaBreached);
        ViewBag.SlaRate = tickets.Count == 0 ? 100 : Math.Round(100.0 * tickets.Count(t => !t.IsSlaBreached) / tickets.Count, 1);
        ViewBag.Mttr = resolved.Count == 0 ? 0 : Math.Round(resolved.Average(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalHours), 1);
        ViewBag.Csat = scores.Count == 0 ? 0 : Math.Round(scores.Average(), 2);

        return View(tickets.Where(t => t.IsSlaBreached && t.ResolvedAt == null).ToList());
    }
}
