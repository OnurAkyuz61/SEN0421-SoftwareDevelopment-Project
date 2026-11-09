using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// Chatbot arka ucu: soruyu bilgi bankasında arar. Karşılık bulunamayan soru
// kaydedilir; yönetim "Cevapsız sorular" listesinden bilgi bankasının
// eksiklerini görür ve makale yazınca soruyu kapatır.
[AuthorizeRole]
public class ChatbotController : Controller
{
    private const int MaxQuestionLength = 500;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ve", "ile", "bir", "bu", "şu", "için", "ama", "gibi", "çok", "daha", "nasıl", "neden",
        "nedir", "niye", "var", "yok", "olan", "olarak", "ben", "benim", "bana", "beni", "sen",
        "biz", "değil", "ediyor", "olmuyor", "yapabilirim", "yardım", "lütfen", "merhaba"
    };

    private readonly AppDbContext _db;
    public ChatbotController(AppDbContext db) => _db = db;

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

        var kelimeler = Regex.Split(soru, @"[^\p{L}\p{N}]+")
            .Where(k => k.Length >= 3 && !StopWords.Contains(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        // Her kelime ayrı aranır; makale kaç kelimeyle eşleştiyse o kadar puan alır
        var puanlar = new Dictionary<int, (string Title, int Score)>();
        foreach (var k in kelimeler)
        {
            var bulunan = await _db.KnowledgeArticles.AsNoTracking()
                .Where(a => a.IsPublished && (a.Title.Contains(k) || a.Body.Contains(k)))
                .Select(a => new { a.Id, a.Title })
                .Take(20)
                .ToListAsync();

            foreach (var a in bulunan)
                puanlar[a.Id] = (a.Title, puanlar.TryGetValue(a.Id, out var p) ? p.Score + 1 : 1);
        }

        // Tek kelimelik soruda bir, diğerlerinde en az iki kelime eşleşmeli
        var esik = kelimeler.Count >= 2 ? 2 : 1;
        var makaleler = puanlar
            .Where(p => p.Value.Score >= esik)
            .OrderByDescending(p => p.Value.Score).ThenBy(p => p.Value.Title)
            .Take(3)
            .Select(p => new { id = p.Key, title = p.Value.Title, url = Url.Action("Details", "Knowledge", new { id = p.Key }) })
            .ToList();

        if (makaleler.Count > 0)
            return Json(new { answered = true, articles = makaleler });

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
            .Where(a => a.IsPublished).OrderBy(a => a.Title)
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
