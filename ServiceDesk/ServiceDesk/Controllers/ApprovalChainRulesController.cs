using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// Onay zinciri adımlarının risk dışındaki ölçütlerle (kategori, öncelik)
// yapılandırılması. Kural, talep onaya girerken (açılış, plan güncelleme,
// yeniden gönderim) ApprovalChainResolver tarafından uygulanır; onay süreci
// başlamış taleplerin adım sayısı değişmez.
[AuthorizeRole(UserRole.Admin)]
public class ApprovalChainRulesController : Controller
{
    private readonly AppDbContext _db;
    public ApprovalChainRulesController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.ApprovalChainRules.AsNoTracking()
            .Include(r => r.Category)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        await LoadCategoriesAsync();
        if (id == null) return View(new ApprovalChainRule { RequiredSteps = 2, IsActive = true });

        var rule = await _db.ApprovalChainRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        return rule == null ? NotFound() : View(rule);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ApprovalChainRule vm)
    {
        ModelState.Remove(nameof(vm.Category));
        if (vm.CategoryId == null && vm.MinPriority == null && vm.Risk == null)
            ModelState.AddModelError(string.Empty, "En az bir koşul (kategori, öncelik veya risk) seçin.");
        if (vm.CategoryId != null && !await _db.Categories.AnyAsync(c => c.Id == vm.CategoryId))
            ModelState.AddModelError(nameof(vm.CategoryId), "Geçerli bir kategori seçin.");

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync();
            return View(vm);
        }

        ApprovalChainRule rule;
        if (vm.Id == 0)
        {
            rule = new ApprovalChainRule();
            _db.ApprovalChainRules.Add(rule);
        }
        else
        {
            rule = await _db.ApprovalChainRules.FirstAsync(r => r.Id == vm.Id);
        }

        rule.Name = vm.Name.Trim();
        rule.CategoryId = vm.CategoryId;
        rule.MinPriority = vm.MinPriority;
        rule.Risk = vm.Risk;
        rule.RequiredSteps = Math.Clamp(vm.RequiredSteps, 1, 5);
        rule.IsActive = vm.IsActive;
        rule.SortOrder = vm.SortOrder;

        await _db.SaveChangesAsync();
        TempData["Ok"] = $"\"{rule.Name}\" kuralı kaydedildi. Yeni ve yeniden gönderilen taleplere uygulanır.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var rule = await _db.ApprovalChainRules.FirstOrDefaultAsync(r => r.Id == id);
        if (rule == null) return NotFound();
        rule.IsActive = !rule.IsActive;
        await _db.SaveChangesAsync();
        TempData["Ok"] = $"\"{rule.Name}\" {(rule.IsActive ? "etkinleştirildi" : "pasifleştirildi")}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadCategoriesAsync()
    {
        var list = await _db.Categories.AsNoTracking()
            .Include(c => c.Parent).Where(c => c.IsActive).ToListAsync();
        ViewBag.Categories = CatalogRules.OrderByHierarchy(list, c => c.Id, c => c.ParentId,
            c => c.SortOrder, c => c.Name);
    }
}
