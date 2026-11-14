using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Search;

namespace ServiceDesk.Controllers;

// Chatbot arka ucu: soruyu bilgi bankasında arar. Karşılık bulunamayan soru
// kaydedilir; yönetim "Cevapsız sorular" listesinden bilgi bankasının
// eksiklerini görür ve makale yazınca soruyu kapatır.
//
// Aramanın kendisi SearchService'te: durak kelimeler, kelimeye bölme ve
// skorlama burada ikinci bir kopya olarak duruyordu (kelime sayan bir
// döngü), aynı soruya bilgi bankası ekranından farklı sıralama üretiyordu.
[AuthorizeRole]
public class ChatbotController : Controller
{
    private const int MaxQuestionLength = 500;

    // Cevap sayılması için gösterilecek en fazla makale
    private const int MaxAnswers = 3;

    private readonly AppDbContext _db;
    private readonly SearchService _search;

    public ChatbotController(AppDbContext db, SearchService search)
    {
        _db = db;
        _search = search;
    }

    public record AskRequest(string? Question);

    // POST /Chatbot/Ask  { "question": "vpn bağlantım kopuyor" }
    // JSON gövdeli istek; başka siteden tarayıcı ile gönderilemez (CORS).
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> Ask([FromBody] AskRequest? req)
    {
        var soru = Regex.Replace(req?.Question ?? "", @"\s+", " ").Trim();
        if (soru.Length < 3) return BadRequest(new { error = "Soru en az 3 karakter olmalı." });
        if (soru.Length > MaxQuestionLength) soru = soru[..MaxQuestionLength];

        // Eşik (tek kelimelik soruda bir, diğerlerinde en az iki kelimenin
        // tutması) ve sıralama SearchService'te; buradaki karar yalnızca
        // "cevap bulundu mu".
        var bulunan = await _search.AnswerArticlesAsync(soru, MaxAnswers);

        if (bulunan.Count > 0)
        {
            var makaleler = bulunan
                .Select(a => new { id = a.Id, title = a.Title, url = Url.Action("Details", "Knowledge", new { id = a.Id }) })
                .ToList();

            return Json(new { answered = true, articles = makaleler });
        }

        // Cevap yok: soru kaydedilir. Aynı soru açık kayıtta varsa sayaç artar.
        var kayit = await _db.ChatbotQuestions.FirstOrDefaultAsync(q => !q.IsResolved && q.Text == soru);
        if (kayit == null)
            _db.ChatbotQuestions.Add(new ChatbotQuestion { Text = soru });
        else
        {
            kayit.AskCount++;
            kayit.LastAskedAt = AppTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        return Json(new
        {
            answered = false,
            message = "Bu konuda bilgi bankasında bir makale bulamadım. Sorunuz destek ekibine iletildi; isterseniz talep açabilirsiniz.",
            createTicketUrl = Url.Action("Create", "Portal")
        });
    }

    // Yönetim: chatbot'un cevaplayamadığı sorular (bilgi bankası eksikleri)
    [AuthorizeRole(UserRole.Agent, UserRole.Admin)]
    public async Task<IActionResult> Unanswered(bool resolved = false, int page = 1)
    {
        var (list, pager) = await _db.ChatbotQuestions.AsNoTracking()
            .Where(q => q.IsResolved == resolved)
            .OrderByDescending(q => q.AskCount).ThenByDescending(q => q.LastAskedAt)
            .ToPageAsync(page);

        ViewBag.Resolved = resolved;
        ViewBag.Pager = pager;
        ViewBag.Articles = await _db.KnowledgeArticles.AsNoTracking()
            .Where(a => a.Status == KnowledgeArticleStatus.Published).OrderBy(a => a.Title)
            .Select(a => new KnowledgeArticle { Id = a.Id, Title = a.Title })
            .ToListAsync();
        return View(list);
    }

    // Soruyu ele alındı olarak kapatır; varsa yazılan makaleye bağlar
    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Agent, UserRole.Admin)]
    public async Task<IActionResult> Resolve(int id, int? articleId)
    {
        var q = await _db.ChatbotQuestions.FirstOrDefaultAsync(x => x.Id == id);
        if (q == null) return NotFound();

        if (articleId != null && !await _db.KnowledgeArticles.AnyAsync(a => a.Id == articleId))
        {
            TempData["Err"] = "Seçilen makale bulunamadı.";
            return RedirectToAction(nameof(Unanswered));
        }

        q.IsResolved = true;
        q.ResolvedAt = AppTime.UtcNow;
        q.ArticleId = articleId;
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Soru ele alındı olarak işaretlendi.";
        return RedirectToAction(nameof(Unanswered));
    }
}
