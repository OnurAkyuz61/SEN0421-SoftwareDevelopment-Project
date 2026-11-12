using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// YÖNETİM: Müşteri memnuniyet anketi sonuçları (ISO 9001 md. 9.1.2)
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public partial class SurveysController : Controller
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

// ───── ŞABLON YÖNETİMİ (yalnızca Admin) ─────────────────────────────────
//
// Sınıf süzgeci Destek + Yönetici'ye açık (yukarıdaki sonuç ekranı); şablon
// yönetimi yalnızca yöneticiye. Aşağıdaki action'larda ayrıca
// [AuthorizeRole(UserRole.Admin)] var: iki süzgeç birlikte çalışır, ikisinin
// de geçilmesi gerekir.
//
// Hiçbir kayıt SİLİNMEZ, pasifleştirilir:
//   • SurveyAnswer.QuestionId geçmiş yanıtlara bağlıdır (DeleteBehavior.Restrict) —
//     soru silinirse eski değerlendirmelerin hangi soruya verildiği kaybolur.
//   • SurveyResponse.TemplateId de aynı şekilde şablonu tutar.
// Pasif soru yeni anketlerde sorulmaz, eski yanıtlarda görünmeye devam eder.
public partial class SurveysController
{
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Templates()
    {
        var templates = await _db.SurveyTemplates.AsNoTracking()
            .Include(t => t.Questions)
            .OrderByDescending(t => t.IsActive).ThenBy(t => t.Name)
            .ToListAsync();

        // Şablon başına kaç değerlendirme toplanmış: pasifleştirmenin ne kadar
        // geçmişi ilgilendirdiği görünsün
        ViewBag.ResponseCounts = await _db.SurveyResponses.AsNoTracking()
            .GroupBy(r => r.TemplateId)
            .Select(g => new { Id = g.Key, Adet = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Adet);

        return View(templates);
    }

    // Yeni şablon (id = 0) ya da yeniden adlandırma.
    //
    // Yeni şablon her zaman PASİF açılır: aktifleştirme tek kapıdan
    // (Activate) geçsin, "tek aktif şablon" kuralı tek yerde yazılı kalsın.
    [HttpPost, ValidateAntiForgeryToken, AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> SaveTemplate(int id, string? name)
    {
        name = CatalogRules.NormalizeName(name);

        if (name.Length == 0)
            return Fail("Anket adı boş olamaz.");
        if (name.Length > SurveyTemplateRules.MaxNameLength)
            return Fail($"Anket adı en fazla {SurveyTemplateRules.MaxNameLength} karakter olabilir.");

        // Aynı ad iki şablonda kullanılamaz: raporda hangi şablonun sonucu
        // olduğu ayırt edilemez ("Anket" / " anket " aynı sayılır)
        var mevcut = await _db.SurveyTemplates.AsNoTracking()
            .Select(t => new { t.Id, t.Name, t.IsActive }).ToListAsync();
        if (CatalogRules.FindDuplicate(mevcut, t => t.Id, t => t.Name, name, id) is { } ayni)
            return Fail($"\"{ayni.Name}\" adında bir anket şablonu zaten var"
                      + (ayni.IsActive ? " (aktif)." : " (pasif). Yeni şablon açmak yerine onu yeniden aktifleştirebilirsiniz."));

        if (id == 0)
        {
            var yeni = new SurveyTemplate { Name = name, IsActive = false };
            _db.SurveyTemplates.Add(yeni);
            await _db.SaveChangesAsync();

            TempData["Ok"] = $"\"{name}\" şablonu pasif olarak eklendi. Soruları ekleyip aktifleştirebilirsiniz.";
            return RedirectToAction(nameof(Questions), new { id = yeni.Id });
        }

        var template = await _db.SurveyTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return NotFound();

        var eskiAd = template.Name;
        template.Name = name;
        await _db.SaveChangesAsync();

        TempData["Ok"] = eskiAd == name ? "Değişiklik yok." : $"\"{eskiAd}\" → \"{name}\" olarak kaydedildi.";
        return RedirectToAction(nameof(Templates));
    }

    // Şablonu aktifleştirir. TEK AKTİF ŞABLON kuralının uygulandığı yer:
    // başka bir şablon aktifse istek reddedilir — sessizce diğerini kapatmak
    // yerine, yöneticinin hangi anketin yürürlükte olduğunu bilerek
    // değiştirmesi gerekir.
    [HttpPost, ValidateAntiForgeryToken, AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Activate(int id)
    {
        var template = await _db.SurveyTemplates
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return NotFound();

        if (template.IsActive) return Fail("Şablon zaten aktif.");

        var digerAktif = await _db.SurveyTemplates.AsNoTracking()
            .Where(t => t.IsActive && t.Id != id)
            .Select(t => t.Name)
            .FirstOrDefaultAsync();

        var hata = SurveyTemplateRules.ActivationError(digerAktif, template.Questions.Count(q => q.IsActive));
        if (hata != null) return Fail(hata, template.Id);

        template.IsActive = true;
        await _db.SaveChangesAsync();

        // Yarış durumu: iki yönetici aynı anda iki ayrı şablonu aktif ederse
        // ikisi de kontrolü geçebilir. Şema değişmediği için veritabanı
        // seviyesinde kısıt yok; kayıttan SONRA bir kez daha bakılır ve kural
        // bozulduysa bu istek kendi değişikliğini geri alır.
        if (await _db.SurveyTemplates.CountAsync(t => t.IsActive) > 1)
        {
            template.IsActive = false;
            await _db.SaveChangesAsync();
            return Fail("Bu sırada başka bir şablon aktifleştirildi. Aynı anda yalnızca bir anket şablonu aktif olabilir.");
        }

        TempData["Ok"] = $"\"{template.Name}\" artık aktif anket şablonu; kapanan talepler bununla değerlendirilecek.";
        return RedirectToAction(nameof(Templates));
    }

    // Şablonu pasifleştirir. Geçmiş yanıtlar yerinde kalır; aktif şablon
    // kalmazsa portalda anket formu hiç görünmez (PortalController aktif
    // şablon bulamaz, kullanıcı hata ekranı görmez).
    [HttpPost, ValidateAntiForgeryToken, AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var template = await _db.SurveyTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return NotFound();

        if (!template.IsActive) return Fail("Şablon zaten pasif.");

        template.IsActive = false;
        await _db.SaveChangesAsync();

        TempData["Ok"] = $"\"{template.Name}\" pasifleştirildi. Aktif şablon kalmadıysa kapanan talepler için anket sorulmaz; "
                       + "toplanmış değerlendirmeler raporlarda durmaya devam eder.";
        return RedirectToAction(nameof(Templates));
    }

    // ───── Soru yönetimi ────────────────────────────────────────────────

    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Questions(int id)
    {
        var template = await _db.SurveyTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return NotFound();

        ViewBag.Template = template;

        // Soru başına kaç yanıt birikmiş: silme yerine pasifleştirmenin
        // neden gerekli olduğu ekranda sayıyla görünsün
        ViewBag.AnswerCounts = await _db.SurveyAnswers.AsNoTracking()
            .Where(a => a.Question!.TemplateId == id)
            .GroupBy(a => a.QuestionId)
            .Select(g => new { Id = g.Key, Adet = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Adet);

        // Pasif sorular da listelenir: yönetici geçmişte neyin sorulduğunu
        // görsün ve gerekirse yeniden açsın
        return View(await OrderedQuestionsAsync(id, tracking: false));
    }

    [HttpPost, ValidateAntiForgeryToken, AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> AddQuestion(int templateId, string? text)
    {
        if (!await _db.SurveyTemplates.AnyAsync(t => t.Id == templateId)) return NotFound();

        text = CatalogRules.NormalizeName(text);
        if (text.Length == 0)
            return Fail("Soru metni boş olamaz.", templateId);
        if (text.Length > SurveyTemplateRules.MaxQuestionLength)
            return Fail($"Soru en fazla {SurveyTemplateRules.MaxQuestionLength} karakter olabilir.", templateId);

        var sorular = await _db.SurveyQuestions.AsNoTracking()
            .Where(q => q.TemplateId == templateId)
            .Select(q => new { q.Id, q.Text, q.SortOrder })
            .ToListAsync();

        // Aynı soru iki kez sorulmasın: kullanıcı aynı şeyi iki kez puanlar,
        // soru bazlı ortalama da bölünür
        if (CatalogRules.FindDuplicate(sorular, q => q.Id, q => q.Text, text, 0) != null)
            return Fail("Bu soru şablonda zaten var.", templateId);

        _db.SurveyQuestions.Add(new SurveyQuestion
        {
            TemplateId = templateId,
            Text = text,
            SortOrder = SurveyTemplateRules.NextSortOrder(sorular.Select(q => q.SortOrder)),
            IsActive = true
        });

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Soru eklendi.";
        return RedirectToAction(nameof(Questions), new { id = templateId });
    }

    // Soruyu listede bir sıra yukarı (-1) / aşağı (+1) taşır.
    //
    // Komşuyla SortOrder takası yapmak yetmez: değerler eşit olabilir (elle
    // girilmiş, seed'den gelmiş) ve takas hiçbir şeyi değiştirmez. Görüntü
    // sırası hesaplanır (SurveyTemplateRules.Move), ardından şablonun tamamı
    // 10, 20, 30 … olarak yeniden numaralandırılır.
    [HttpPost, ValidateAntiForgeryToken, AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> MoveQuestion(int id, int delta)
    {
        var sahip = await _db.SurveyQuestions.AsNoTracking()
            .Where(q => q.Id == id).Select(q => (int?)q.TemplateId).FirstOrDefaultAsync();
        if (sahip == null) return NotFound();

        if (delta is not (-1 or 1)) return Fail("Geçersiz taşıma yönü.", sahip);

        var sorular = await OrderedQuestionsAsync(sahip.Value, tracking: true);
        var yeniSira = SurveyTemplateRules.Move(sorular.Select(q => q.Id).ToList(), id, delta);

        for (var i = 0; i < yeniSira.Count; i++)
            sorular.First(q => q.Id == yeniSira[i]).SortOrder = (i + 1) * SurveyTemplateRules.SortStep;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Questions), new { id = sahip });
    }

    // Soruyu pasifleştirir / yeniden açar. SİLME YOK: SurveyAnswer.QuestionId
    // geçmiş yanıtlara bağlı (DeleteBehavior.Restrict), silmek veri
    // bütünlüğünü bozar. Pasif soru yeni anketlerde sorulmaz, eski
    // değerlendirmelerde görünmeye devam eder.
    [HttpPost, ValidateAntiForgeryToken, AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> ToggleQuestion(int id)
    {
        var question = await _db.SurveyQuestions
            .Include(q => q.Template)
            .FirstOrDefaultAsync(q => q.Id == id);
        if (question == null) return NotFound();

        if (question.IsActive)
        {
            var digerAktif = await _db.SurveyQuestions
                .CountAsync(q => q.TemplateId == question.TemplateId && q.IsActive && q.Id != id);

            var hata = SurveyTemplateRules.QuestionDeactivationError(question.Template!.IsActive, digerAktif);
            if (hata != null) return Fail(hata, question.TemplateId);
        }

        question.IsActive = !question.IsActive;
        await _db.SaveChangesAsync();

        TempData["Ok"] = question.IsActive
            ? "Soru yeniden ankete alındı."
            : "Soru pasifleştirildi; yeni anketlerde sorulmayacak, geçmiş yanıtlar korunuyor.";
        return RedirectToAction(nameof(Questions), new { id = question.TemplateId });
    }

    // ───── Yardımcılar ──────────────────────────────────────────────────

    // Görüntü sırası: SortOrder, eşitlikte Id — sıra her sayfada aynı olsun
    private async Task<List<SurveyQuestion>> OrderedQuestionsAsync(int templateId, bool tracking)
    {
        var q = _db.SurveyQuestions.Where(x => x.TemplateId == templateId);
        if (!tracking) q = q.AsNoTracking();
        return await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync();
    }

    // templateId verilirse soru sayfasına, verilmezse şablon listesine döner
    private IActionResult Fail(string message, int? templateId = null)
    {
        TempData["Err"] = message;
        return templateId == null
            ? RedirectToAction(nameof(Templates))
            : RedirectToAction(nameof(Questions), new { id = templateId });
    }
}

public class QuestionAverage
{
    public string Text { get; set; } = "";
    public int Count { get; set; }
    public double Average { get; set; }
}
