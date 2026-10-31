using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// YÖNETİM: Müşteri memnuniyet anketi sonuçları (ISO 9001 md. 9.1.2)
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class SurveysController : Controller
{
    private readonly AppDbContext _db;
    public SurveysController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(int page = 1)
    {
        // KPI'lar tüm yanıtlardan; tablo yalnızca ilgili sayfa (Paging)
        var tumPuanlar = await _db.SurveyAnswers.AsNoTracking()
            .Select(a => new { a.QuestionId, a.Score })
            .ToListAsync();

        ViewBag.Avg = tumPuanlar.Count == 0 ? 0 : Math.Round(tumPuanlar.Average(a => a.Score), 2);
        ViewBag.Dist = Enumerable.Range(1, 5)
            .Select(s => tumPuanlar.Count(a => a.Score == s)).ToArray();
        ViewBag.TotalResponses = await _db.SurveyResponses.CountAsync();

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

        foreach (var qa in (List<QuestionAverage>)ViewBag.ByQuestion)
        {
            var puanlar = tumPuanlar.Where(a => a.QuestionId == qa.Count).Select(a => a.Score).ToList();
            qa.Count = puanlar.Count;
            qa.Average = puanlar.Count == 0 ? 0 : Math.Round(puanlar.Average(), 2);
        }

        var (list, pager) = await _db.SurveyResponses
            .AsNoTracking()
            .Include(r => r.Ticket)
            .Include(r => r.SubmittedBy)
            .Include(r => r.Answers).ThenInclude(a => a.Question)
            .OrderByDescending(r => r.CreatedAt)
            .ToPageAsync(page);

        ViewBag.Pager = pager;
        return View(list);
    }
}

public class QuestionAverage
{
    public string Text { get; set; } = "";
    public int Count { get; set; }
    public double Average { get; set; }
}
