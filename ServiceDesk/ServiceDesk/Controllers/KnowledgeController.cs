using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// Bilgi bankası (ISO 9001 md. 7.5 dokümante edilmiş bilgi).
// Okuma herkese açık: kullanıcı talebi açmadan önce çözümü kendi bulabilsin.
// Yazma yalnızca destek ve yöneticide.
[AuthorizeRole]
public class KnowledgeController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUser _current;

    public KnowledgeController(AppDbContext db, CurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IActionResult> Index(string? q, int? categoryId)
    {
        var query = _db.KnowledgeArticles.AsNoTracking().Include(a => a.Category).AsQueryable();

        // Son kullanıcı yalnızca yayımlanmış makaleleri görür
        if (!_current.IsStaff) query = query.Where(a => a.IsPublished);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(a => a.Title.Contains(q) || a.Body.Contains(q));
        if (categoryId != null)
            query = query.Where(a => a.CategoryId == categoryId);

        ViewBag.Q = q;
        ViewBag.CategoryId = categoryId;
        ViewBag.Categories = await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();

        return View(await query
            .OrderByDescending(a => a.UsedCount).ThenBy(a => a.Title)
            .ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var article = await _db.KnowledgeArticles.AsNoTracking()
            .Include(a => a.Category).Include(a => a.Author)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (article == null) return NotFound();
        if (!article.IsPublished && !_current.IsStaff) return NotFound();

        return View(article);
    }

    [AuthorizeRole(UserRole.Agent, UserRole.Admin)]
    public async Task<IActionResult> Edit(int? id)
    {
        await LoadCategoriesAsync();

        if (id == null) return View(new KnowledgeArticle());

        // Yazar bilgisi sağdaki "Bilgi" kartında gösteriliyor
        var article = await _db.KnowledgeArticles.Include(a => a.Author).FirstOrDefaultAsync(a => a.Id == id);
        return article == null ? NotFound() : View(article);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Agent, UserRole.Admin)]
    public async Task<IActionResult> Edit(int? id, string? title, string? body, int? categoryId, bool isPublished)
    {
        string? hata = null;
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            hata = "Başlık ve içerik boş olamaz.";
        else if (title.Trim().Length > 200)
            hata = "Başlık en fazla 200 karakter olabilir.";

        if (hata != null)
        {
            TempData["Err"] = hata;
            await LoadCategoriesAsync();
            return View(new KnowledgeArticle
            {
                Id = id ?? 0, Title = title ?? "", Body = body ?? "",
                CategoryId = categoryId, IsPublished = isPublished
            });
        }

        KnowledgeArticle article;
        if (id is > 0)
        {
            var mevcut = await _db.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == id);
            if (mevcut == null) return NotFound();
            article = mevcut;
            article.UpdatedAt = AppTime.UtcNow;
        }
        else
        {
            article = new KnowledgeArticle { AuthorId = _current.User!.Id, CreatedAt = AppTime.UtcNow };
            _db.KnowledgeArticles.Add(article);
        }

        article.Title = title!.Trim();
        article.Body = body!.Trim();
        article.CategoryId = categoryId;
        article.IsPublished = isPublished;

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Makale kaydedildi.";
        return RedirectToAction(nameof(Details), new { id = article.Id });
    }

    // Talebi bu makaleyle çözüldü olarak işaretler; makalenin kullanım sayacı artar
    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Agent, UserRole.Admin)]
    public async Task<IActionResult> LinkToTicket(int ticketId, int? articleId, string? rowVersion)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket == null) return NotFound();
        if (TicketConcurrency.Expect(_db, ticket, rowVersion)) return await ConflictAsync(ticketId);

        // Aynı makale tekrar seçilirse sayaç şişmesin
        if (ticket.ResolvedByArticleId == articleId)
            return RedirectToAction("Details", "Tickets", new { id = ticketId });

        // Önceki makalenin sayacı geri alınır
        if (ticket.ResolvedByArticleId != null)
        {
            var onceki = await _db.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == ticket.ResolvedByArticleId);
            if (onceki != null && onceki.UsedCount > 0) onceki.UsedCount--;
        }

        if (articleId != null)
        {
            var yeni = await _db.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == articleId);
            if (yeni == null) return NotFound();
            yeni.UsedCount++;
        }

        ticket.ResolvedByArticleId = articleId;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TicketConcurrency.DiscardChanges(_db);
            return await ConflictAsync(ticketId);
        }

        TempData["Ok"] = articleId == null ? "Makale bağlantısı kaldırıldı." : "Talep makaleye bağlandı.";
        return RedirectToAction("Details", "Tickets", new { id = ticketId });
    }

    private async Task<IActionResult> ConflictAsync(int ticketId)
    {
        TempData["Err"] = await TicketConcurrency.ConflictMessageAsync(_db, ticketId, _current.User?.Id);
        return RedirectToAction("Details", "Tickets", new { id = ticketId });
    }

    private async Task LoadCategoriesAsync() =>
        ViewBag.Categories = await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();
}
