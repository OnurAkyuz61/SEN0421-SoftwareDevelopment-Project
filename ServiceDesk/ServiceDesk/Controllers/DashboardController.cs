using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// YÖNETİM PANELİ: ISO 9001 süreç performans göstergeleri + bekleyen işler
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var tickets = await _db.Tickets.Include(t => t.Assignee).Include(t => t.Category).ToListAsync();
        var categories = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();
        var feedback = await _db.Feedbacks.Include(f => f.Ticket).OrderByDescending(f => f.CreatedAt).ToListAsync();

        bool IsOpen(Ticket t) => t.Status is TicketStatus.New or TicketStatus.InProgress;
        var resolved = tickets.Where(t => t.ResolvedAt != null).ToList();
        var today = AppTime.LocalToday;
        var days = Enumerable.Range(0, 14).Select(i => today.AddDays(i - 13)).ToList();

        var vm = new DashboardVm
        {
            Total = tickets.Count,
            Open = tickets.Count(IsOpen),
            Unassigned = tickets.Count(t => IsOpen(t) && t.AssigneeId == null),
            PendingApproval = tickets.Count(t => t.ApprovalStatus == ApprovalStatus.Pending),
            Breached = tickets.Count(t => IsOpen(t) && t.IsSlaBreached),
            SlaRate = tickets.Count == 0 ? 100 : Math.Round(100.0 * tickets.Count(t => !t.IsSlaBreached) / tickets.Count, 1),
            Mttr = resolved.Count == 0 ? 0 : Math.Round(resolved.Average(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalHours), 1),
            Csat = feedback.Count == 0 ? 0 : Math.Round(feedback.Average(f => f.Score), 2),

            StatusLabels = Enum.GetValues<TicketStatus>().Select(Ui.StatusText).ToArray(),
            StatusCounts = Enum.GetValues<TicketStatus>().Select(s => tickets.Count(t => t.Status == s)).ToArray(),
            CategoryLabels = categories.Select(c => c.Name).ToArray(),
            CategoryCounts = categories.Select(c => tickets.Count(t => t.CategoryId == c.Id)).ToArray(),
            DayLabels = days.Select(d => d.ToString("dd.MM")).ToArray(),
            // Tarihler UTC saklandığı için günlük kırılım yerel takvime çevrilerek sayılır
            DayCreated = days.Select(d => tickets.Count(t => AppTime.ToLocal(t.CreatedAt).Date == d)).ToArray(),
            DayResolved = days.Select(d => tickets.Count(t => AppTime.ToLocal(t.ResolvedAt)?.Date == d)).ToArray(),

            Approvals = tickets.Where(t => t.ApprovalStatus == ApprovalStatus.Pending).OrderBy(t => t.CreatedAt).Take(5).ToList(),
            UnassignedList = tickets.Where(t => IsOpen(t) && t.AssigneeId == null).OrderBy(t => t.SlaDueAt).Take(5).ToList(),
            BreachedList = tickets.Where(t => IsOpen(t) && t.IsSlaBreached).OrderBy(t => t.SlaDueAt).Take(5).ToList(),
            LatestFeedback = feedback.Take(5).ToList()
        };
        return View(vm);
    }
}
