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
    private readonly SessionRegistry _sessions;
    private readonly PasswordHistoryService _passwordHistory;
    private readonly AuditService _audit;

    public UsersController(AppDbContext db, CurrentUser current,
                           SessionRegistry sessions, PasswordHistoryService passwordHistory,
                           AuditService audit)
    {
        _db = db;
        _current = current;
        _sessions = sessions;
        _passwordHistory = passwordHistory;
        _audit = audit;
    }

    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> Index(string? q, UserRole? role, bool? active, int page = 1)
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

        // Son giriş denemeleri: güvenlik denetim izi. Kimin hangi IP'den, hangi
        // e-postayla ve ne sonuçla giriş denediği hassas bir kayıttır (var olmayan
        // hesaplara yapılan denemeler, kilitlenen hesaplar). Liste sayfası destek
        // personeline de açık olduğu için bu bölüm yalnızca yöneticiye yüklenir.
        if (_current.User!.Role == UserRole.Admin)
            ViewBag.LoginAttempts = await _db.LoginAttempts.AsNoTracking()
                .OrderByDescending(l => l.AttemptedAt).Take(20).ToListAsync();

        var (list, pager) = await query
            .OrderBy(u => u.Role).ThenBy(u => u.FirstName)
            .ToPageAsync(page);

        ViewBag.Pager = pager;
        return View(list);
    }

    // Giriş denemesi denetim izinin tamamı: yalnızca yönetici.
    // Filtreler: e-posta (içerir), sonuç, yalnızca başarısızlar, tarih aralığı.
    [HttpGet]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> LoginAttempts(string? q, LoginResult? result, bool failedOnly = false,
                                                   DateTime? from = null, DateTime? to = null)
    {
        const int Limit = 500;
        var query = _db.LoginAttempts.AsNoTracking().Include(l => l.User).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ara = q.Trim().ToLowerInvariant();
            query = query.Where(l => l.Email.Contains(ara) || (l.IpAddress != null && l.IpAddress.Contains(ara)));
        }
        if (result != null) query = query.Where(l => l.Result == result);
        if (failedOnly) query = query.Where(l => l.Result != LoginResult.Success && l.Result != LoginResult.DevLogin);
        // Form yerel tarih gönderir, kayıtlar UTC
        if (from != null)
        {
            var bas = AppTime.ToUtc(from.Value.Date);
            query = query.Where(l => l.AttemptedAt >= bas);
        }
        if (to != null)
        {
            var bit = AppTime.ToUtc(to.Value.Date.AddDays(1));
            query = query.Where(l => l.AttemptedAt < bit);
        }

        ViewBag.Q = q;
        ViewBag.Result = result;
        ViewBag.FailedOnly = failedOnly;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Limit = Limit;
        ViewBag.Total = await query.CountAsync();

        // Son 24 saatin özeti: kilitlenen hesaplar ve en çok başarısız deneme alan adresler
        var gun = AppTime.UtcNow.AddDays(-1);
        ViewBag.Failed24h = await _db.LoginAttempts.CountAsync(l => l.AttemptedAt >= gun &&
            l.Result != LoginResult.Success && l.Result != LoginResult.DevLogin);
        ViewBag.Locked24h = await _db.LoginAttempts.CountAsync(l => l.AttemptedAt >= gun && l.Result == LoginResult.LockedOut);
        ViewBag.TopTargets = await _db.LoginAttempts.AsNoTracking()
            .Where(l => l.AttemptedAt >= gun && l.Result != LoginResult.Success && l.Result != LoginResult.DevLogin)
            .GroupBy(l => l.Email)
            .Select(g => new LoginTargetRow(g.Key, g.Count()))
            .OrderByDescending(x => x.Count).Take(5).ToListAsync();

        return View(await query.OrderByDescending(l => l.AttemptedAt).Take(Limit).ToListAsync());
    }

    public record LoginTargetRow(string Email, int Count);

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
        var phone = PhoneRules.Normalize(vm.PhoneNumber);

        // Şifre yeni kayıtta zorunlu; düzenlemede boş bırakılırsa değişmez.
        // Doluysa karmaşıklık kuralları (PasswordRules / StrongPassword) uygulanır.
        if (vm.IsNew && string.IsNullOrEmpty(vm.Password))
            ModelState.AddModelError(nameof(vm.Password), "Yeni hesap için şifre belirleyin");
        else if (!string.IsNullOrEmpty(vm.Password) && PasswordRules.Validate(vm.Password) is { } pwdErr)
            ModelState.AddModelError(nameof(vm.Password), pwdErr);
        else if (!string.IsNullOrEmpty(vm.Password) && user != null &&
                 await _passwordHistory.ReuseErrorAsync(user, vm.Password) is { } reuseErr)
            ModelState.AddModelError(nameof(vm.Password), reuseErr);

        if (await _db.Users.AnyAsync(u => u.Email == vm.Email && u.Id != vm.Id))
            ModelState.AddModelError(nameof(vm.Email), "Bu e-posta adresi başka bir hesapta kayıtlı");

        if (PhoneRules.Validate(phone) is { } phoneError)
            ModelState.AddModelError(nameof(vm.PhoneNumber), phoneError);
        else if (phone != null && await PhoneTakenAsync(phone, vm.Id))
            ModelState.AddModelError(nameof(vm.PhoneNumber), "Bu telefon numarası başka bir hesapta kayıtlı");

        // Aktif hesabın kayıtlı numarası boşaltılamaz: OTP gidecek yer kalmaz ve
        // kişi giriş yapamaz. (Telefonsuz yeni hesap açmak hâlâ mümkün; listede
        // "telefon yok" uyarısıyla görünür.)
        if (user != null && PhoneRules.WouldLockOut(user.PhoneNumber, phone, vm.IsActive))
        {
            ModelState.AddModelError(nameof(vm.PhoneNumber), PhoneRules.RemoveBlockedMessage);
            phone = user.PhoneNumber;
        }

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

        // Ekip lideri yalnızca aktif Destek / Yönetici olabilir (CatalogRules.CanLeadTeam).
        // Liderin rolü Kullanıcı'ya düşürülür ya da hesabı pasifleştirilirse
        // ekip, eskalasyonu alamayan bir lidere bağlı kalırdı.
        if (user != null && (vm.Role == UserRole.User || !vm.IsActive) &&
            await LedTeamNamesAsync(user.Id) is { Count: > 0 } liderOlduguEkipler)
        {
            ModelState.AddModelError(vm.Role == UserRole.User ? nameof(vm.Role) : nameof(vm.IsActive),
                $"Bu kişi {string.Join(", ", liderOlduguEkipler)} ekibinin lideri. " +
                "Önce Katalog ekranından liderliği başka bir Destek/Yönetici hesabına devredin.");
        }

        if (!ModelState.IsValid)
        {
            await LoadFormListsAsync();
            vm.PhoneNumber = phone;
            return View(vm);
        }

        var yeniKayit = user == null;
        var pasiflestirildi = user is { IsActive: true } && !vm.IsActive;
        var rolDegisti = user != null && user.Role != vm.Role;
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

        // Yönetici belirlediği şifre geçicidir: kullanıcı ilk girişte değiştirir
        if (!string.IsNullOrEmpty(vm.Password))
            await _passwordHistory.SetPasswordAsync(user, vm.Password, requireChangeOnNextLogin: true);

        await _db.SaveChangesAsync();

        if (pasiflestirildi)
            await _sessions.CloseAllAsync(user.Id);

        var sifreBelirlendi = !string.IsNullOrEmpty(vm.Password);
        TempData["Ok"] = yeniKayit
            ? $"{user.FullName} ({Ui.RoleText(user.Role)}) hesabı oluşturuldu. İlk girişte şifre değiştirmesi istenecek."
            : pasiflestirildi
                ? $"{user.FullName} hesabı pasifleştirildi. Talep geçmişi korundu, açık oturumları kapatıldı."
                : rolDegisti
                    ? $"{user.FullName} rolü {Ui.RoleText(user.Role)} olarak güncellendi. Açık oturumundaki yetkiler bir sonraki istekte yenilenir."
                    : sifreBelirlendi
                        ? $"{user.FullName} hesabı güncellendi. Yeni şifre geçici; bir sonraki girişte değiştirmesi istenecek."
                        : $"{user.FullName} hesabı güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    // Yönetici, hesabı pasifleştirmeden tüm açık oturumları düşürür.
    // Hedef kullanıcı bir sonraki isteğinde giriş ekranına yönlendirilir.
    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> ForceLogout(int id, string? returnUrl)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (user.Id == _current.User!.Id)
        {
            TempData["Err"] = "Kendi oturumunuzu bu düğmeyle kapatamazsınız; çıkış yapın.";
        }
        else
        {
            var kapatilan = await _sessions.CloseAllAsync(user.Id);
            await _audit.WriteAsync(AuditAction.ForceLogout, _current.User.Id, _current.User.Email,
                $"Yönetici zorla çıkış: {user.FullName} (#{user.Id}), {kapatilan} oturum kapatıldı.");

            TempData["Ok"] = kapatilan == 0
                ? $"{user.FullName} için açık oturum bulunamadı."
                : $"{user.FullName} hesabının {kapatilan} açık oturumu sonlandırıldı.";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl!);
        return RedirectToAction(nameof(Edit), new { id });
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
        else if (!isActive && await LedTeamNamesAsync(user.Id) is { Count: > 0 } ekipler)
        {
            TempData["Err"] = $"{user.FullName} {string.Join(", ", ekipler)} ekibinin lideri; " +
                              "pasifleştirmeden önce Katalog ekranından liderliği devredin.";
        }
        else if (user.IsActive == isActive)
        {
            TempData["Ok"] = $"{user.FullName} hesabı zaten {(isActive ? "aktif" : "pasif")}.";
        }
        else
        {
            user.IsActive = isActive;
            await _db.SaveChangesAsync();

            if (!isActive)
                await _sessions.CloseAllAsync(user.Id);

            TempData["Ok"] = isActive
                ? $"{user.FullName} hesabı yeniden aktifleştirildi."
                : $"{user.FullName} hesabı pasifleştirildi. Talep geçmişi korundu; açık oturumları kapatıldı.";
        }

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    private Task<bool> OtherActiveAdminExistsAsync(int exceptUserId) =>
        _db.Users.AnyAsync(u => u.Id != exceptUserId && u.Role == UserRole.Admin && u.IsActive);

    private async Task<bool> PhoneTakenAsync(string phone, int exceptUserId)
    {
        var others = await _db.Users.AsNoTracking()
            .Where(u => u.Id != exceptUserId && u.PhoneNumber != null)
            .Select(u => u.PhoneNumber).ToListAsync();
        return others.Any(o => PhoneRules.Normalize(o) == phone);
    }

    private Task<List<string>> LedTeamNamesAsync(int userId) =>
        _db.Teams.Where(t => t.LeadUserId == userId).OrderBy(t => t.Name).Select(t => t.Name).ToListAsync();

    private async Task LoadFormListsAsync()
    {
        ViewBag.Departments = await _db.Departments.AsNoTracking()
            .Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
        ViewBag.Teams = await _db.Teams.AsNoTracking()
            .Include(t => t.Department)
            .Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();
    }
}
