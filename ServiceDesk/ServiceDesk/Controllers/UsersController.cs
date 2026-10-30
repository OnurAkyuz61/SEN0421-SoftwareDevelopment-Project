using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// YÖNETİM: kullanıcı listesi, oluşturma, düzenleme, rol ve aktiflik yönetimi.
//
// Listeyi destek personeli de görür (kime atama yapacağını bilmesi gerekir),
// ancak hesap oluşturmak, rol vermek ve hesabı pasifleştirmek yalnızca
// yöneticinin yetkisindedir.
//
// Kayıt SİLİNMEZ, pasifleştirilir (ISO 9001 md. 7.5): ayrılan kişinin
// talepleri, yazışmaları ve onayları geçmişte bağlı kalır. Pasif hesap
// CurrentUser tarafından oturum açmış sayılmaz, yani erişimi anında kesilir.
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class UsersController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUser _current;
    private readonly IPasswordHasher<AppUser> _hasher;

    public UsersController(AppDbContext db, CurrentUser current, IPasswordHasher<AppUser> hasher)
    {
        _db = db;
        _current = current;
        _hasher = hasher;
    }

    public async Task<IActionResult> Index(string? q, UserRole? role, bool? active)
    {
        var query = _db.Users.AsNoTracking()
            .Include(u => u.Department)
            .Include(u => u.Team)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ara = q.Trim();
            query = query.Where(u => u.FirstName.Contains(ara) || u.LastName.Contains(ara)
                                     || u.Email.Contains(ara));
        }
        if (role != null) query = query.Where(u => u.Role == role);
        if (active != null) query = query.Where(u => u.IsActive == active);

        ViewBag.Q = q;
        ViewBag.Role = role;
        ViewBag.Active = active;
        ViewBag.PassiveCount = await _db.Users.CountAsync(u => !u.IsActive);

        // Son giriş denemeleri: güvenlik denetim izi
        ViewBag.LoginAttempts = await _db.LoginAttempts.AsNoTracking()
            .OrderByDescending(l => l.AttemptedAt).Take(20).ToListAsync();

        return View(await query
            .OrderBy(u => u.Role).ThenBy(u => u.FirstName)
            .ToListAsync());
    }

    // Oluşturma ve düzenleme aynı ekran: id boşsa yeni kayıt
    [HttpGet]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Edit(int? id)
    {
        await LoadFormListsAsync();

        if (id == null) return View(new UserFormVm());

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var vm = UserFormVm.From(user);
        vm.OpenTicketCount = await _db.Tickets.CountAsync(t =>
            t.AssigneeId == user.Id &&
            (t.Status == TicketStatus.New || t.Status == TicketStatus.InProgress));

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Edit(UserFormVm vm)
    {
        var me = _current.User!;

        AppUser? user = null;
        if (!vm.IsNew)
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Id == vm.Id);
            if (user == null) return NotFound();
        }

        vm.Email = vm.Email.Trim();
        var phone = string.IsNullOrWhiteSpace(vm.PhoneNumber) ? null : vm.PhoneNumber.Trim();

        // Şifre yeni kayıtta zorunlu; düzenlemede boş bırakılırsa değişmez
        if (vm.IsNew && string.IsNullOrEmpty(vm.Password))
            ModelState.AddModelError(nameof(vm.Password), "Yeni hesap için şifre belirleyin");

        if (await _db.Users.AnyAsync(u => u.Email == vm.Email && u.Id != vm.Id))
            ModelState.AddModelError(nameof(vm.Email), "Bu e-posta adresi başka bir hesapta kayıtlı");

        if (phone != null && await _db.Users.AnyAsync(u => u.PhoneNumber == phone && u.Id != vm.Id))
            ModelState.AddModelError(nameof(vm.PhoneNumber), "Bu telefon numarası başka bir hesapta kayıtlı");

        // Ekip seçildiyse gerçekten var olmalı ve seçilen birime bağlı olmalı
        if (vm.TeamId != null)
        {
            var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == vm.TeamId);
            if (team == null)
                ModelState.AddModelError(nameof(vm.TeamId), "Geçerli bir ekip seçin");
            else if (vm.DepartmentId != null && team.DepartmentId != vm.DepartmentId)
                ModelState.AddModelError(nameof(vm.TeamId), "Seçilen ekip bu birime bağlı değil");
            else if (vm.DepartmentId == null)
                vm.DepartmentId = team.DepartmentId;   // ekip birimi belirler
        }

        if (vm.DepartmentId != null && !await _db.Departments.AnyAsync(d => d.Id == vm.DepartmentId))
            ModelState.AddModelError(nameof(vm.DepartmentId), "Geçerli bir birim seçin");

        // Yönetici kendi yetkisini ve erişimini elinden alamaz: aksi halde
        // sistemde yönetici kalmadan kilitlenme yaşanabilir
        if (user != null && user.Id == me.Id)
        {
            if (vm.Role != user.Role)
                ModelState.AddModelError(nameof(vm.Role),
                    "Kendi rolünüzü değiştiremezsiniz; bunu başka bir yönetici yapmalı");
            if (!vm.IsActive)
                ModelState.AddModelError(nameof(vm.IsActive), "Kendi hesabınızı pasifleştiremezsiniz");
        }

        // Son aktif yönetici korunur
        if (user is { Role: UserRole.Admin, IsActive: true } &&
            (vm.Role != UserRole.Admin || !vm.IsActive) &&
            !await OtherActiveAdminExistsAsync(user.Id))
        {
            ModelState.AddModelError(string.Empty,
                "Bu hesap sistemdeki tek aktif yönetici. Önce başka bir yöneticiyi yetkilendirin.");
        }

        if (!ModelState.IsValid)
        {
            await LoadFormListsAsync();
            vm.PhoneNumber = phone;
            return View(vm);
        }

        var yeniKayit = user == null;
        if (user == null)
        {
            user = new AppUser { CreatedAt = AppTime.UtcNow };
            _db.Users.Add(user);
        }

        user.FirstName = vm.FirstName.Trim();
        user.LastName = vm.LastName.Trim();
        user.Email = vm.Email;
        user.PhoneNumber = phone;
        user.Role = vm.Role;
        user.DepartmentId = vm.DepartmentId;
        user.TeamId = vm.TeamId;
        user.IsActive = vm.IsActive;

        if (!string.IsNullOrEmpty(vm.Password))
            user.PasswordHash = _hasher.HashPassword(user, vm.Password);

        await _db.SaveChangesAsync();

        TempData["Ok"] = yeniKayit
            ? $"{user.FullName} ({Ui.RoleText(user.Role)}) hesabı oluşturuldu."
            : $"{user.FullName} hesabı güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    // Listeden hızlı aktif / pasif değişimi
    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> SetActive(int id, bool isActive, string? returnUrl)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (user.Id == _current.User!.Id && !isActive)
        {
            TempData["Err"] = "Kendi hesabınızı pasifleştiremezsiniz.";
        }
        else if (!isActive && user is { Role: UserRole.Admin, IsActive: true } &&
                 !await OtherActiveAdminExistsAsync(user.Id))
        {
            TempData["Err"] = "Sistemdeki tek aktif yönetici pasifleştirilemez.";
        }
        else if (user.IsActive == isActive)
        {
            TempData["Ok"] = $"{user.FullName} hesabı zaten {(isActive ? "aktif" : "pasif")}.";
        }
        else
        {
            user.IsActive = isActive;
            await _db.SaveChangesAsync();

            TempData["Ok"] = isActive
                ? $"{user.FullName} hesabı yeniden aktifleştirildi."
                : $"{user.FullName} hesabı pasifleştirildi. Talep geçmişi korundu, oturumu kapatıldı.";
        }

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    private Task<bool> OtherActiveAdminExistsAsync(int exceptUserId) =>
        _db.Users.AnyAsync(u => u.Id != exceptUserId && u.Role == UserRole.Admin && u.IsActive);

    private async Task LoadFormListsAsync()
    {
        ViewBag.Departments = await _db.Departments.AsNoTracking()
            .Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
        ViewBag.Teams = await _db.Teams.AsNoTracking()
            .Include(t => t.Department)
            .Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();
    }
}
