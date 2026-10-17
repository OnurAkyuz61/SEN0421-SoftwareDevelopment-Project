using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;

namespace ServiceDesk.Controllers;

// YÖNETİM: Müşteri memnuniyet anketi sonuçları (ISO 9001 md. 9.1.2)
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class SurveysController : Controller
{
    private readonly AppDbContext _db;
    public SurveysController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var list = await _db.SurveyResponses
            .AsNoTracking()
            .Include(r => r.Ticket)
            .Include(r => r.SubmittedBy)
            .Include(r => r.Answers).ThenInclude(a => a.Question)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var tumPuanlar = list.SelectMany(r => r.Answers).ToList();

        ViewBag.Avg = tumPuanlar.Count == 0 ? 0 : Math.Round(tumPuanlar.Average(a => a.Score), 2);
        // 1..5 puan dağılımı
        ViewBag.Dist = Enumerable.Range(1, 5)
            .Select(s => tumPuanlar.Count(a => a.Score == s)).ToArray();

        // Soru bazında ortalama: hangi boyutta zayıfız
        ViewBag.ByQuestion = await _db.SurveyQuestions
            .AsNoTracking()
            .Where(q => q.IsActive)
            .OrderBy(q => q.SortOrder)
            .Select(q => new QuestionAverage
            {
                Text = q.Text,
                Count = q.Id,
                Average = 0
            })
            .ToListAsync();

        // Ortalamalar bellekte hesaplanır (soru sayısı az)
        foreach (var qa in (List<QuestionAverage>)ViewBag.ByQuestion)
        {
            var puanlar = tumPuanlar.Where(a => a.QuestionId == qa.Count).Select(a => a.Score).ToList();
            qa.Count = puanlar.Count;
            qa.Average = puanlar.Count == 0 ? 0 : Math.Round(puanlar.Average(), 2);
        }

        return View(list);
    }
}

public class QuestionAverage
{
    public string Text { get; set; } = "";
    public int Count { get; set; }
    public double Average { get; set; }
}
