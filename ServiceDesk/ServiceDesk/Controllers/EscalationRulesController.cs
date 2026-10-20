using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;

namespace ServiceDesk.Controllers;

// SLA ihlalinden sonra talebin hangi gecikmede hangi role yükseleceği.
// Seviye benzersizdir; SlaMonitorService bu kuralları dakikada bir uygular.
[AuthorizeRole(UserRole.Admin)]
public class EscalationRulesController : Controller
{
    private readonly AppDbContext _db;
    public EscalationRulesController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var list = await _db.EscalationRules.AsNoTracking()
            .OrderBy(r => r.Level).ToListAsync();
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return View(new EscalationRule { Level = 1, AfterMinutes = 0, TargetRole = UserRole.Agent, IsActive = true });

        var rule = await _db.EscalationRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        return rule == null ? NotFound() : View(rule);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EscalationRule vm)
    {
        if (vm.Level < 1)
            ModelState.AddModelError(nameof(vm.Level), "Seviye en az 1 olmalı.");
        if (vm.AfterMinutes < 0)
            ModelState.AddModelError(nameof(vm.AfterMinutes), "Gecikme negatif olamaz.");
        if (vm.TargetRole == UserRole.User)
            ModelState.AddModelError(nameof(vm.TargetRole), "Hedef rol Destek veya Yönetici olmalı.");

        var duplicate = await _db.EscalationRules
            .AnyAsync(r => r.Level == vm.Level && r.Id != vm.Id);
        if (duplicate)
            ModelState.AddModelError(nameof(vm.Level), "Bu seviye numarası zaten kullanılıyor.");

        if (!ModelState.IsValid) return View(vm);

        EscalationRule rule;
        if (vm.Id == 0)
        {
            rule = new EscalationRule();
            _db.EscalationRules.Add(rule);
        }
        else
        {
            rule = await _db.EscalationRules.FirstAsync(r => r.Id == vm.Id);
        }

        rule.Level = vm.Level;
        rule.AfterMinutes = vm.AfterMinutes;
        rule.TargetRole = vm.TargetRole;
        rule.IsActive = vm.IsActive;

        await _db.SaveChangesAsync();
        TempData["Ok"] = $"Seviye {rule.Level} kuralı kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var rule = await _db.EscalationRules.FirstOrDefaultAsync(r => r.Id == id);
        if (rule == null) return NotFound();

        rule.IsActive = !rule.IsActive;
        await _db.SaveChangesAsync();
        TempData["Ok"] = $"Seviye {rule.Level} {(rule.IsActive ? "etkinleştirildi" : "pasifleştirildi")}.";
        return RedirectToAction(nameof(Index));
    }
}
