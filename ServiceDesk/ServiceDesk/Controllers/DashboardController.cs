using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Controllers;

// YÖNETİM PANELİ: ISO 9001 süreç performans göstergeleri + bekleyen işler
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var tickets = await _db.Tickets.ToListAsync();
        var feedback = await _db.Feedbacks.Include(f => f.Ticket).OrderByDescending(f => f.CreatedAt).ToListAsync();

        bool IsOpen(Ticket t) => t.Status is TicketStatus.New or TicketStatus.InProgress;
        var resolved = tickets.Where(t => t.ResolvedAt != null).ToList();
        var today = DateTime.Today;
        var days = Enumerable.Range(0, 14).Select(i => today.AddDays(i - 13)).ToList();

        var vm = new DashboardVm
        {
            Total = tickets.Count,
            Open = tickets.Count(IsOpen),
            Unassigned = tickets.Count(t => IsOpen(t) && t.AssignedTo == null),
            PendingApproval = tickets.Count(t => t.ApprovalStatus == ApprovalStatus.Pending),
            Breached = tickets.Count(t => IsOpen(t) && t.IsSlaBreached),
            SlaRate = tickets.Count == 0 ? 100 : Math.Round(100.0 * tickets.Count(t => !t.IsSlaBreached) / tickets.Count, 1),
            Mttr = resolved.Count == 0 ? 0 : Math.Round(resolved.Average(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalHours), 1),
            Csat = feedback.Count == 0 ? 0 : Math.Round(feedback.Average(f => f.Score), 2),

            StatusLabels = Enum.GetValues<TicketStatus>().Select(Ui.StatusText).ToArray(),
            StatusCounts = Enum.GetValues<TicketStatus>().Select(s => tickets.Count(t => t.Status == s)).ToArray(),
            CategoryLabels = Enum.GetValues<TicketCategory>().Select(Ui.CategoryText).ToArray(),
            CategoryCounts = Enum.GetValues<TicketCategory>().Select(c => tickets.Count(t => t.Category == c)).ToArray(),
            DayLabels = days.Select(d => d.ToString("dd.MM")).ToArray(),
            DayCreated = days.Select(d => tickets.Count(t => t.CreatedAt.Date == d)).ToArray(),
            DayResolved = days.Select(d => tickets.Count(t => t.ResolvedAt?.Date == d)).ToArray(),

            Approvals = tickets.Where(t => t.ApprovalStatus == ApprovalStatus.Pending).OrderBy(t => t.CreatedAt).Take(5).ToList(),
            UnassignedList = tickets.Where(t => IsOpen(t) && t.AssignedTo == null).OrderBy(t => t.SlaDueAt).Take(5).ToList(),
            BreachedList = tickets.Where(t => IsOpen(t) && t.IsSlaBreached).OrderBy(t => t.SlaDueAt).Take(5).ToList(),
            LatestFeedback = feedback.Take(5).ToList()
        };
        return View(vm);
    }
}
