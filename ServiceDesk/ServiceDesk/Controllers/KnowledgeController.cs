using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Search;

namespace ServiceDesk.Controllers;

// Bilgi bankası (ISO 9001 md. 7.5 dokümante edilmiş bilgi).
// Okuma herkese açık: kullanıcı talebi açmadan önce çözümü kendi bulabilsin —
// ama yalnızca YAYINDAKİ makale. Yazma destek ve yöneticide, yayına çıkarma
// yetkisi yalnızca yöneticide.
//
// Görünürlük ve geçiş kuralları KnowledgePublishing'de; bu controller yalnızca
// isteği karşılar, kararı oraya sorar.
[AuthorizeRole]
public class KnowledgeController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUser _current;
    private readonly SearchService _search;

    public KnowledgeController(AppDbContext db, CurrentUser current, SearchService search)
    {
        _db = db;
        _current = current;
        _search = search;
    }

    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> Index(string? q, int? categoryId, KnowledgeArticleStatus? status, int page = 1)
    {
        // Arama, görünürlük kapsamı ve sıralama SearchService'te: eskiden
        // buradaki "Title.Contains(q) || Body.Contains(q)" hiçbir sıralama
        // üretmiyordu, sonuçlar kullanım sayacına göre diziliyordu — yani
        // arama kelimesiyle alakası en yüksek makale listenin sonunda
        // kalabiliyordu.
        var query = _search.Articles(q, categoryId);

        // Durum süzgeci yalnızca personel için anlamlı: son kullanıcının
        // görebildiği tek durum zaten "Yayında"
        if (status != null && _current.IsStaff)
            query = query.Where(a => a.Status == status);

        ViewBag.Q = q;
        ViewBag.CategoryId = categoryId;
        ViewBag.Status = status;
        ViewBag.Categories = await LoadCategoryListAsync();

        // Onay kuyruğunun büyüklüğü: yönetici listeye girdiğinde bekleyen iş
        // olduğunu görsün
        ViewBag.PendingCount = _current.Role == UserRole.Admin
            ? await _db.KnowledgeArticles.CountAsync(a => a.Status == KnowledgeArticleStatus.PendingApproval)
            : 0;

        // Sıralama eklenmiyor: sorgu SearchService'ten sıralı geliyor
        // (aramada skora göre, aramasız girişte en çok kullanılan üstte).
        // Buraya bir OrderBy daha yazılırsa skor sıralamasını EZER.
        var (list, pager) = await query.ToPageAsync(page);

        ViewBag.Pager = pager;
        return View(list);
    }

    public async Task<IActionResult> Details(int id)
    {
        var article = await _db.KnowledgeArticles.AsNoTracking()
            .Include(a => a.Category).Include(a => a.Author)
            .Include(a => a.SubmittedBy).Include(a => a.ApprovedBy)
            .Include(a => a.Revisions.OrderByDescending(r => r.VersionNo))
                .ThenInclude(r => r.CreatedBy)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (article == null) return NotFound();

        // Görme yetkisi olmayan kullanıcıya makalenin varlığı bile
        // sızdırılmaz: 403 yerine 404
        if (!KnowledgePublishing.CanView(article, _current.User)) return NotFound();

        // Kullanıcının kendi oyu: düğmelerden hangisinin seçili görüneceği
        ViewBag.MyVote = _current.Id is { } meId
            ? await _db.KnowledgeArticleFeedbacks.AsNoTracking()
                .Where(f => f.ArticleId == id && f.UserId == meId)
                .Select(f => (bool?)f.IsHelpful)
                .FirstOrDefaultAsync()
            : null;

        return View(article);
    }

    [AuthorizeRole(UserRole.Agent, UserRole.Admin)]
    public async Task<IActionResult> Edit(int? id)
    {
        await LoadCategoriesAsync();

        if (id == null) return View(new KnowledgeArticle());

        // Yazar bilgisi sağdaki "Bilgi" kartında gösteriliyor
        var article = await _db.KnowledgeArticles
            .Include(a => a.Author).Include(a => a.ApprovedBy)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (article == null) return NotFound();

        var yetki = KnowledgePublishing.CanEdit(article, _current.User);
        if (!yetki.Allowed)
        {
            TempData["Err"] = yetki.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(article);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Agent, UserRole.Admin)]
    public async Task<IActionResult> Edit(int? id, string? title, string? body, int? categoryId)
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

            // Durum kartı doğru uyarıyı göstersin: yayındaki makalenin
            // formu "taslak" gibi görünmesin
            var durum = id is > 0
                ? await _db.KnowledgeArticles.Where(a => a.Id == id)
                      .Select(a => a.Status).FirstOrDefaultAsync()
                : KnowledgeArticleStatus.Draft;

            return View(new KnowledgeArticle
            {
                Id = id ?? 0, Title = title ?? "", Body = body ?? "",
                CategoryId = categoryId, Status = durum
            });
        }

        var now = AppTime.UtcNow;
        var me = _current.User!;

        KnowledgeArticle article;
        bool yeni = id is not > 0;
        bool icerikDegisti;

        if (!yeni)
        {
            var mevcut = await _db.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == id);
            if (mevcut == null) return NotFound();

            var yetki = KnowledgePublishing.CanEdit(mevcut, me);
            if (!yetki.Allowed)
            {
                TempData["Err"] = yetki.Error;
                return RedirectToAction(nameof(Details), new { id });
            }

            article = mevcut;

            // Durum geçişi içeriğin gerçekten değişip değişmediğine bağlı:
            // yalnızca kaydet'e basmak yayındaki makaleyi onaya düşürmemeli
            icerikDegisti = article.Title != title!.Trim()
                            || article.Body != body!.Trim()
                            || article.CategoryId != categoryId;

            if (icerikDegisti) article.UpdatedAt = now;
        }
        else
        {
            article = new KnowledgeArticle
            {
                AuthorId = me.Id,
                CreatedAt = now,
                Status = KnowledgeArticleStatus.Draft   // yeni makale taslak doğar
            };
            _db.KnowledgeArticles.Add(article);
            icerikDegisti = true;
        }

        var oncekiDurum = article.Status;

        article.Title = title!.Trim();
        article.Body = body!.Trim();
        article.CategoryId = categoryId;

        KnowledgePublishing.ApplyEdit(article, me, icerikDegisti, now);

        // Her kaydetme yeni bir sürüm bırakır (md. 7.5.3.2). İçerik
        // değişmediyse yazılmaz: geçmiş aynı metnin kopyalarıyla şişmesin.
        if (icerikDegisti)
        {
            var sira = KnowledgePublishing.NextVersionNo(yeni ? null : await MaxVersionNoAsync(article.Id));
            article.Revisions.Add(KnowledgePublishing.Snapshot(
                article, sira, me.Id, now,
                yeni ? "İlk sürüm" : null));
        }

        await _db.SaveChangesAsync();

        TempData["Ok"] = oncekiDurum == KnowledgeArticleStatus.Published
                         && article.Status == KnowledgeArticleStatus.PendingApproval
            ? "Makale kaydedildi ve onaya gönderildi. Onaylanana kadar son kullanıcıya görünmez."
            : "Makale kaydedildi.";

        return RedirectToAction(nameof(Details), new { id = article.Id });
    }

    // ───── Yayın onay süreci ─────────────────────────────────────────────

    // Taslağı (ya da reddedilen makaleyi) onaya gönderir
    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Agent, UserRole.Admin)]
    public async Task<IActionResult> Submit(int id)
    {
        var article = await _db.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == id);
        if (article == null) return NotFound();

        var karar = KnowledgePublishing.CanSubmit(article, _current.User);
        if (!karar.Allowed)
        {
            TempData["Err"] = karar.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        KnowledgePublishing.Submit(article, _current.User!, AppTime.UtcNow);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Makale onaya gönderildi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Onaylar ve yayına alır
    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Approve(int id)
    {
        var article = await _db.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == id);
        if (article == null) return NotFound();

        var karar = KnowledgePublishing.CanDecide(article, _current.User);
        if (!karar.Allowed)
        {
            TempData["Err"] = karar.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        KnowledgePublishing.Approve(article, _current.User!, AppTime.UtcNow);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Makale onaylandı ve yayına alındı.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Reddeder; gerekçe zorunlu
    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Reject(int id, string? note)
    {
        var article = await _db.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == id);
        if (article == null) return NotFound();

        var karar = KnowledgePublishing.CanReject(article, _current.User, note);
        if (!karar.Allowed)
        {
            TempData["Err"] = karar.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        KnowledgePublishing.Reject(article, _current.User!, note!, AppTime.UtcNow);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Makale reddedildi; gerekçe yazara iletildi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ───── Sürüm geçmişi ─────────────────────────────────────────────────

    // Seçilen sürümün içeriğini makaleye geri yazar. Geri dönüş de bir
    // değişikliktir: yeni bir sürüm olarak kaydedilir ve yayındaki makale
    // onaya geri döner.
    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Agent, UserRole.Admin)]
    public async Task<IActionResult> Revert(int id, int revisionId)
    {
        var article = await _db.KnowledgeArticles
            .Include(a => a.Revisions)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (article == null) return NotFound();

        var yetki = KnowledgePublishing.CanEdit(article, _current.User);
        if (!yetki.Allowed)
        {
            TempData["Err"] = yetki.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        // Sürüm bu makaleye ait olmalı: başka makalenin sürüm numarasıyla
        // içerik taşınamaz
        var surum = article.Revisions.FirstOrDefault(r => r.Id == revisionId);
        if (surum == null) return NotFound();

        if (!KnowledgePublishing.DiffersFrom(surum, article))
        {
            TempData["Err"] = $"{surum.VersionNo}. sürüm makalenin şu anki haliyle aynı.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = AppTime.UtcNow;
        var me = _current.User!;
        var oncekiDurum = article.Status;

        KnowledgePublishing.Restore(article, surum);
        article.UpdatedAt = now;
        KnowledgePublishing.ApplyEdit(article, me, true, now);

        var sira = KnowledgePublishing.NextVersionNo(article.Revisions);
        article.Revisions.Add(KnowledgePublishing.Snapshot(
            article, sira, me.Id, now, $"{surum.VersionNo}. sürüme geri dönüldü"));

        await _db.SaveChangesAsync();

        TempData["Ok"] = oncekiDurum == KnowledgeArticleStatus.Published
            ? $"{surum.VersionNo}. sürüme geri dönüldü; makale yeniden onay bekliyor."
            : $"{surum.VersionNo}. sürüme geri dönüldü.";

        return RedirectToAction(nameof(Details), new { id });
    }

    // Eski bir sürümün tam metni. Makaleyi değiştirmez, yalnızca gösterir.
    [AuthorizeRole(UserRole.Agent, UserRole.Admin)]
    public async Task<IActionResult> Revision(int id, int revisionId)
    {
        var article = await _db.KnowledgeArticles.AsNoTracking()
            .Include(a => a.Category)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (article == null) return NotFound();

        if (!KnowledgePublishing.CanView(article, _current.User)) return NotFound();

        var surum = await _db.KnowledgeArticleRevisions.AsNoTracking()
            .Include(r => r.CreatedBy).Include(r => r.Category)
            .FirstOrDefaultAsync(r => r.Id == revisionId && r.ArticleId == id);
        if (surum == null) return NotFound();

        ViewBag.Article = article;
        return View(surum);
    }

    // ───── Okuyucu geri bildirimi ────────────────────────────────────────

    // "Faydalı" / "faydalı değil". Oy veren herkesin bir makalede tek oyu
    // olur; aynı düğmeye tekrar basmak bir şey değiştirmez, diğerine basmak
    // oyu çevirir.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Feedback(int id, bool helpful)
    {
        var me = _current.User!;

        // Yalnızca durum gerekiyor: makalenin kendisi izlenmeden okunur,
        // çünkü sayaçlar aşağıda tek SQL UPDATE ile değişecek ve izlenen
        // bir kopya o UPDATE'ten sonra bayat kalırdı.
        var durum = await _db.KnowledgeArticles.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => (KnowledgeArticleStatus?)a.Status)
            .FirstOrDefaultAsync();

        if (durum == null) return NotFound();

        // Oy yalnızca yayındaki makaleye verilir: taslak henüz okunacak bir
        // belge değil, oyu da kurumsal bilgi sayılmaz
        if (durum != KnowledgeArticleStatus.Published)
        {
            TempData["Err"] = "Yalnızca yayındaki bir makaleye geri bildirim verilebilir.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var mevcut = await _db.KnowledgeArticleFeedbacks
            .FirstOrDefaultAsync(f => f.ArticleId == id && f.UserId == me.Id);

        if (mevcut != null && mevcut.IsHelpful == helpful)
        {
            TempData["Ok"] = "Geri bildiriminiz zaten kayıtlı.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = AppTime.UtcNow;
        var yeniOy = mevcut == null;

        // Sayaçlara uygulanacak fark oy satırı yazılmadan ÖNCE hesaplanır:
        // yeni oy (+1), oy değişimi (-1 / +1)
        var fark = KnowledgeUsagePolicy.Delta(mevcut?.IsHelpful, helpful);

        if (yeniOy)
        {
            _db.KnowledgeArticleFeedbacks.Add(new KnowledgeArticleFeedback
            {
                ArticleId = id, UserId = me.Id, IsHelpful = helpful, CreatedAt = now
            });
        }
        else
        {
            mevcut!.IsHelpful = helpful;
            mevcut.UpdatedAt = now;
        }

        // Oy satırı ile özet sayaçlar ya birlikte yazılır ya hiç: sayaçlar
        // SaveChanges'ten bağımsız, hemen çalışan bir UPDATE ile değişiyor.
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (yeniOy)
        {
            // Aynı kullanıcı iki sekmeden aynı anda oy verdi: ArticleId+UserId
            // benzersiz dizini ikinci satırı reddetti. İlk oy kayıtlı.
            await tx.RollbackAsync();
            TicketConcurrency.DiscardChanges(_db);
            TempData["Err"] = "Bu makaleye zaten oy verdiniz; oyunuz değiştirilmedi.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await ApplyVoteDeltaAsync(id, fark);
        await tx.CommitAsync();

        TempData["Ok"] = helpful
            ? "Geri bildiriminiz alındı: makale faydalı."
            : "Geri bildiriminiz alındı: makale faydalı değil. Eksik kalan noktayı talep açarak yazabilirsiniz.";

        return RedirectToAction(nameof(Details), new { id });
    }

    // Özet oy sayaçlarını tek SQL UPDATE ile değiştirir: artırma /
    // azaltmayı veritabanı yapar, sayaç belleğe okunup geri yazılmaz.
    //
    // NOT: sayaçları oy tablosundan yeniden SAYDIRMAK (SetProperty içinde
    // "SELECT COUNT(*) FROM Feedbacks") daha dirençli olurdu ama EF Core 8
    // SetProperty değerinde alt sorgu çeviremiyor ("could not be
    // translated"). Fark eklemek de aynı ölçüde atomiktir; oy satırı ile
    // sayaç aynı işlemde (transaction) değiştiği ve kullanıcı başına tek oy
    // kuralını benzersiz dizin koruduğu için fark her zaman gerçek satır
    // değişikliğine karşılık gelir.
    private Task ApplyVoteDeltaAsync(int articleId, KnowledgeUsagePolicy.VoteDelta delta)
    {
        if (delta.IsEmpty) return Task.CompletedTask;

        var helpful = delta.Helpful;
        var notHelpful = delta.NotHelpful;

        return _db.KnowledgeArticles
            .Where(a => a.Id == articleId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.HelpfulCount, a => a.HelpfulCount + helpful)
                .SetProperty(a => a.NotHelpfulCount, a => a.NotHelpfulCount + notHelpful));
    }

    // Talebi bu makaleyle çözüldü olarak işaretler; makalenin kullanım
    // sayacı artar.
    //
    // Sayaçlar oku-değiştir-yaz ile değil tek SQL UPDATE ile değişir:
    // "yeni.UsedCount++" iki destek personeli aynı anda makale bağladığında
    // artışlardan birini kaybediyordu (ikisi de aynı değeri okuyup aynı
    // değeri yazıyordu). ExecuteUpdateAsync artışı veritabanına yaptırır.
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

        var oncekiId = ticket.ResolvedByArticleId;

        if (articleId != null)
        {
            // Yalnızca durum okunur, makale izlenmez: sayacı aşağıdaki
            // UPDATE değiştirecek, izlenen kopya bayat kalmasın
            var durum = await _db.KnowledgeArticles.AsNoTracking()
                .Where(a => a.Id == articleId)
                .Select(a => (KnowledgeArticleStatus?)a.Status)
                .FirstOrDefaultAsync();

            if (durum == null) return NotFound();

            // Talep yalnızca yayındaki makaleyle çözülmüş sayılabilir:
            // kullanıcıya gönderilen çözüm onaylanmamış bir taslak olmamalı
            if (durum != KnowledgeArticleStatus.Published)
            {
                TempData["Err"] = "Yalnızca yayındaki bir makale talebin çözümü olarak gösterilebilir.";
                return RedirectToAction("Details", "Tickets", new { id = ticketId });
            }
        }

        ticket.ResolvedByArticleId = articleId;
        var now = AppTime.UtcNow;

        // Talebin kaydı ile sayaçlar ya birlikte uygulanır ya hiç.
        // ExecuteUpdateAsync SaveChanges'i beklemeden çalıştığı için, sürüm
        // çakışması kaydı geri çevirdiğinde sayaçların da geri alınması
        // gerekir; ikisi tek işlemde (transaction) tutulur.
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Sürüm kontrolü burada: çakışma varsa sayaçlara hiç dokunulmaz
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            TicketConcurrency.DiscardChanges(_db);
            return await ConflictAsync(ticketId);
        }

        // Önceki makalenin sayacı geri alınır. "UsedCount > 0" koşulu
        // WHERE'in parçası: sayaç sıfırın altına düşemez ve kontrol ile
        // azaltma arasında yarış kalmaz.
        if (oncekiId != null)
            await _db.KnowledgeArticles
                .Where(a => a.Id == oncekiId.Value && a.UsedCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.UsedCount, a => a.UsedCount - 1));

        if (articleId != null)
            await _db.KnowledgeArticles
                .Where(a => a.Id == articleId.Value)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.UsedCount, a => a.UsedCount + 1)
                    .SetProperty(a => a.LastUsedAt, now));

        await tx.CommitAsync();

        TempData["Ok"] = articleId == null ? "Makale bağlantısı kaldırıldı." : "Talep makaleye bağlandı.";
        return RedirectToAction("Details", "Tickets", new { id = ticketId });
    }

    private async Task<IActionResult> ConflictAsync(int ticketId)
    {
        TempData["Err"] = await TicketConcurrency.ConflictMessageAsync(_db, ticketId, _current.User?.Id);
        return RedirectToAction("Details", "Tickets", new { id = ticketId });
    }

    // Makalenin en yüksek sürüm numarası. Sürüm metinlerinin tamamını
    // belleğe çekmemek için ayrı sorguyla okunur.
    private Task<int?> MaxVersionNoAsync(int articleId) =>
        _db.KnowledgeArticleRevisions
            .Where(r => r.ArticleId == articleId)
            .MaxAsync(r => (int?)r.VersionNo);

    private async Task LoadCategoriesAsync() =>
        ViewBag.Categories = await LoadCategoryListAsync();

    private async Task<List<Category>> LoadCategoryListAsync()
    {
        var list = await _db.Categories.AsNoTracking()
            .Include(c => c.Parent).Where(c => c.IsActive).ToListAsync();
        return CatalogRules.OrderByHierarchy(list, c => c.Id, c => c.ParentId,
            c => c.SortOrder, c => c.Name);
    }
}
