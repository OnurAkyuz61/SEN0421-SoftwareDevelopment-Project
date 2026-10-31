using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// YÖNETİM: Kategori, birim ve ekip yönetimi.
// Bunlar enum değil tablo olduğu için yeni kayıt eklemek kod değişikliği
// gerektirmez; bu ekran olmadan ise veritabanına elle yazmak gerekirdi.
// Kayıtlar silinmez, pasifleştirilir: geçmiş talepler bağlı kalır.
//
// İsimler benzersizdir (kural CatalogRules'ta). Kontrol iki katlı: önce burada
// anlaşılır bir mesajla, ardından veritabanındaki benzersiz indeksle; iki
// yönetici aynı anda aynı adı kaydederse ikincisi indekse takılır ve yine
// aynı mesajı görür.
[AuthorizeRole(UserRole.Admin)]
public class CatalogController : Controller
{
    private readonly AppDbContext _db;
    public CatalogController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        ViewBag.Departments = await _db.Departments.AsNoTracking()
            .OrderBy(d => d.Name).ToListAsync();
        ViewBag.Teams = await _db.Teams.AsNoTracking()
            .Include(t => t.Department).Include(t => t.LeadUser)
            .OrderBy(t => t.Name).ToListAsync();

        // Ekip lideri adayları: yalnızca aktif destek personeli ve yöneticiler
        ViewBag.LeadCandidates = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && (u.Role == UserRole.Agent || u.Role == UserRole.Admin))
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .ToListAsync();

        // Her kategoride kaç talep var: pasifleştirmenin etkisi görünsün
        ViewBag.TicketCounts = await _db.Tickets.AsNoTracking()
            .GroupBy(t => t.CategoryId)
            .Select(g => new { Id = g.Key, Adet = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Adet);

        return View(await _db.Categories.AsNoTracking()
            .Include(c => c.DefaultTeam)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(int id, string? name, int? defaultTeamId, int sortOrder, bool isActive)
    {
        name = CatalogRules.NormalizeName(name);
        if (NameError("Kategori", name) is { } hata) return Fail(hata);

        var mevcut = await _db.Categories.AsNoTracking().Select(c => new { c.Id, c.Name, c.IsActive }).ToListAsync();
        if (CatalogRules.FindDuplicate(mevcut, c => c.Id, c => c.Name, name, id) is { } ayni)
            return Fail(DuplicateMessage("kategori", ayni.Name, ayni.IsActive));

        if (defaultTeamId != null && !await _db.Teams.AnyAsync(t => t.Id == defaultTeamId))
            return Fail("Geçerli bir varsayılan ekip seçin.");

        var kategori = id > 0 ? await _db.Categories.FirstOrDefaultAsync(c => c.Id == id) : null;
        if (id > 0 && kategori == null) return NotFound();

        if (kategori == null)
        {
            kategori = new Category();
            _db.Categories.Add(kategori);
        }

        kategori.Name = name;
        kategori.DefaultTeamId = defaultTeamId;
        kategori.SortOrder = sortOrder;
        kategori.IsActive = isActive;

        return await SaveAsync(kategori.Name, "kategori");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDepartment(int id, string? name, bool isActive)
    {
        name = CatalogRules.NormalizeName(name);
        if (NameError("Birim", name) is { } hata) return Fail(hata);

        var mevcut = await _db.Departments.AsNoTracking().Select(d => new { d.Id, d.Name, d.IsActive }).ToListAsync();
        if (CatalogRules.FindDuplicate(mevcut, d => d.Id, d => d.Name, name, id) is { } ayni)
            return Fail(DuplicateMessage("birim", ayni.Name, ayni.IsActive));

        var birim = id > 0 ? await _db.Departments.FirstOrDefaultAsync(d => d.Id == id) : null;
        if (id > 0 && birim == null) return NotFound();

        if (birim == null)
        {
            birim = new Department();
            _db.Departments.Add(birim);
        }

        birim.Name = name;
        birim.IsActive = isActive;

        return await SaveAsync(birim.Name, "birim");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTeam(int id, string? name, int departmentId, int? leadUserId, bool isActive)
    {
        name = CatalogRules.NormalizeName(name);
        if (NameError("Ekip", name) is { } hata) return Fail(hata);

        var mevcut = await _db.Teams.AsNoTracking().Select(t => new { t.Id, t.Name, t.IsActive }).ToListAsync();
        if (CatalogRules.FindDuplicate(mevcut, t => t.Id, t => t.Name, name, id) is { } ayni)
            return Fail(DuplicateMessage("ekip", ayni.Name, ayni.IsActive));

        if (!await _db.Departments.AnyAsync(d => d.Id == departmentId))
            return Fail("Geçerli bir birim seçin.");

        var ekip = id > 0 ? await _db.Teams.FirstOrDefaultAsync(t => t.Id == id) : null;
        if (id > 0 && ekip == null) return NotFound();

        // Lider yalnızca aktif Destek veya Yönetici olabilir. Ekranda zaten
        // yalnızca bu kişiler listelenir; bu kontrol adresin doğrudan POST
        // edilmesine karşı asıl engeldir. Mevcut lider sonradan pasifleşmiş ya
        // da rolü düşürülmüşse, aynı lider tekrar gönderildiğinde de reddedilir:
        // kayıt geçersiz bir durumda bırakılmaz.
        if (leadUserId != null)
        {
            var lider = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == leadUserId);
            if (!CatalogRules.CanLeadTeam(lider))
                return Fail(lider == null
                    ? "Seçilen ekip lideri bulunamadı."
                    : $"{lider.FullName} ekip lideri olamaz: ekip lideri yalnızca aktif Destek veya Yönetici rolündeki bir kişi olabilir.");
        }

        if (ekip == null)
        {
            ekip = new Team();
            _db.Teams.Add(ekip);
        }

        ekip.Name = name;
        ekip.DepartmentId = departmentId;
        ekip.LeadUserId = leadUserId;
        ekip.IsActive = isActive;

        return await SaveAsync(ekip.Name, "ekip");
    }

    // ───── Yardımcılar ───────────────────────────────────────────────────

    private static string? NameError(string tur, string name) =>
        name.Length == 0 ? $"{tur} adı boş olamaz."
        : name.Length > CatalogRules.MaxNameLength ? $"{tur} adı en fazla {CatalogRules.MaxNameLength} karakter olabilir."
        : null;

    private static string DuplicateMessage(string tur, string mevcutAd, bool aktif) =>
        aktif
            ? $"\"{mevcutAd}\" adında bir {tur} zaten var. Aynı isimde ikinci bir {tur} eklenemez."
            : $"\"{mevcutAd}\" adında pasif bir {tur} zaten var. Yeni kayıt açmak yerine onu yeniden aktifleştirin.";

    private IActionResult Fail(string message)
    {
        TempData["Err"] = message;
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(string name, string tur)
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (AppDbContext.IsUniqueViolation(ex))
        {
            // Kontrolden sonra, kayıttan önce başka bir yönetici aynı adı kaydetti
            return Fail($"\"{name}\" adında bir {tur} zaten var. Aynı isimde ikinci bir {tur} eklenemez.");
        }

        TempData["Ok"] = $"\"{name}\" kaydedildi.";
        return RedirectToAction(nameof(Index));
    }
}
