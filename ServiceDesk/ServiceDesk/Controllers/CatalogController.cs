using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;

namespace ServiceDesk.Controllers;

// YÖNETİM: Kategori, birim ve ekip yönetimi.
// Bunlar enum değil tablo olduğu için yeni kayıt eklemek kod değişikliği
// gerektirmez; bu ekran olmadan ise veritabanına elle yazmak gerekirdi.
// Kayıtlar silinmez, pasifleştirilir: geçmiş talepler bağlı kalır.
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
            .Include(t => t.Department).OrderBy(t => t.Name).ToListAsync();

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
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Err"] = "Kategori adı boş olamaz.";
            return RedirectToAction(nameof(Index));
        }

        var kategori = id > 0 ? await _db.Categories.FirstOrDefaultAsync(c => c.Id == id) : null;
        if (id > 0 && kategori == null) return NotFound();

        if (kategori == null)
        {
            kategori = new Category();
            _db.Categories.Add(kategori);
        }

        kategori.Name = name.Trim();
        kategori.DefaultTeamId = defaultTeamId;
        kategori.SortOrder = sortOrder;
        kategori.IsActive = isActive;

        await _db.SaveChangesAsync();
        TempData["Ok"] = $"\"{kategori.Name}\" kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDepartment(int id, string? name, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Err"] = "Birim adı boş olamaz.";
            return RedirectToAction(nameof(Index));
        }

        var birim = id > 0 ? await _db.Departments.FirstOrDefaultAsync(d => d.Id == id) : null;
        if (id > 0 && birim == null) return NotFound();

        if (birim == null)
        {
            birim = new Department();
            _db.Departments.Add(birim);
        }

        birim.Name = name.Trim();
        birim.IsActive = isActive;

        await _db.SaveChangesAsync();
        TempData["Ok"] = $"\"{birim.Name}\" kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTeam(int id, string? name, int departmentId, int? leadUserId, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Err"] = "Ekip adı boş olamaz.";
            return RedirectToAction(nameof(Index));
        }

        if (!await _db.Departments.AnyAsync(d => d.Id == departmentId))
        {
            TempData["Err"] = "Geçerli bir birim seçin.";
            return RedirectToAction(nameof(Index));
        }

        var ekip = id > 0 ? await _db.Teams.FirstOrDefaultAsync(t => t.Id == id) : null;
        if (id > 0 && ekip == null) return NotFound();

        if (ekip == null)
        {
            ekip = new Team();
            _db.Teams.Add(ekip);
        }

        ekip.Name = name.Trim();
        ekip.DepartmentId = departmentId;
        ekip.LeadUserId = leadUserId;
        ekip.IsActive = isActive;

        await _db.SaveChangesAsync();
        TempData["Ok"] = $"\"{ekip.Name}\" kaydedildi.";
        return RedirectToAction(nameof(Index));
    }
}
