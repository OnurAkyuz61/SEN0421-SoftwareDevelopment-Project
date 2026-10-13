using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;

namespace ServiceDesk.Controllers;

// YÖNETİM: Müşteri memnuniyet anketi sonuçları
public class SurveysController : Controller
{
    private readonly AppDbContext _db;
    public SurveysController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var list = await _db.Feedbacks.Include(f => f.Ticket)
            .OrderByDescending(f => f.CreatedAt).ToListAsync();

        ViewBag.Avg = list.Count == 0 ? 0 : Math.Round(list.Average(f => f.Score), 2);
        // 1..5 puan dağılımı
        ViewBag.Dist = Enumerable.Range(1, 5).Select(s => list.Count(f => f.Score == s)).ToArray();
        return View(list);
    }
}
