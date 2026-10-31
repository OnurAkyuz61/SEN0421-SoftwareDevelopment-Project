using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// Oturum açmış kullanıcının kendi profili: ad, telefon, şifre.
// E-posta ve rol yalnızca yönetim tarafından değiştirilir.
[AuthorizeRole]
public class ProfileController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUser _current;
    private readonly IPasswordHasher<AppUser> _hasher;

    public ProfileController(AppDbContext db, CurrentUser current, IPasswordHasher<AppUser> hasher)
    {
        _db = db;
        _current = current;
        _hasher = hasher;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var me = _current.User!;
        return View(Page(ToVm(me), new ChangePasswordVm()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update([Bind(Prefix = nameof(ProfilePageVm.Profile))] ProfileVm vm)
    {
        var me = await _db.Users.FirstAsync(u => u.Id == _current.Id);

        if (!TryValidateModel(vm, nameof(ProfilePageVm.Profile)))
            return View("Index", Page(FillReadOnly(vm, me), new ChangePasswordVm()));

        var phoneKey = $"{nameof(ProfilePageVm.Profile)}.{nameof(vm.PhoneNumber)}";
        var phone = PhoneRules.Normalize(vm.PhoneNumber);

        // Numara silinirse bir sonraki girişte OTP gönderilecek yer kalmaz ve
        // kullanıcı kendi hesabına giremez: boşaltmak yerine değiştirmeli
        if (PhoneRules.WouldLockOut(me.PhoneNumber, phone, me.IsActive))
        {
            ModelState.AddModelError(phoneKey, PhoneRules.RemoveBlockedMessage);
            vm.PhoneNumber = me.PhoneNumber;
            return View("Index", Page(FillReadOnly(vm, me), new ChangePasswordVm()));
        }

        if (PhoneRules.Validate(phone) is { } phoneError)
        {
            ModelState.AddModelError(phoneKey, phoneError);
            return View("Index", Page(FillReadOnly(vm, me), new ChangePasswordVm()));
        }

        if (phone != null && await PhoneTakenAsync(phone, me.Id))
        {
            ModelState.AddModelError(phoneKey, "Bu telefon numarası başka bir hesapta kayıtlı.");
            return View("Index", Page(FillReadOnly(vm, me), new ChangePasswordVm()));
        }

        me.FirstName = vm.FirstName.Trim();
        me.LastName = vm.LastName.Trim();
        me.PhoneNumber = phone;
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Profil bilgileriniz güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = nameof(ProfilePageVm.Password))] ChangePasswordVm vm)
    {
        var me = await _db.Users.FirstAsync(u => u.Id == _current.Id);

        if (!TryValidateModel(vm, nameof(ProfilePageVm.Password)))
            return View("Index", Page(ToVm(me), vm));

        if (_hasher.VerifyHashedPassword(me, me.PasswordHash, vm.CurrentPassword)
            == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError($"{nameof(ProfilePageVm.Password)}.{nameof(vm.CurrentPassword)}",
                "Mevcut şifre hatalı.");
            return View("Index", Page(ToVm(me), vm));
        }

        me.PasswordHash = _hasher.HashPassword(me, vm.NewPassword);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Şifreniz değiştirildi.";
        return RedirectToAction(nameof(Index));
    }

    // Kayıtlı numaralar farklı biçimlerde girilmiş olabilir ("0532 123 45 67"):
    // karşılaştırma normalize edilmiş halleri üzerinden yapılır
    private async Task<bool> PhoneTakenAsync(string phone, int exceptUserId)
    {
        var others = await _db.Users.AsNoTracking()
            .Where(u => u.Id != exceptUserId && u.PhoneNumber != null)
            .Select(u => u.PhoneNumber).ToListAsync();
        return others.Any(o => PhoneRules.Normalize(o) == phone);
    }

    private static ProfilePageVm Page(ProfileVm profile, ChangePasswordVm password) =>
        new() { Profile = profile, Password = password };

    private static ProfileVm ToVm(AppUser u) => new()
    {
        FirstName = u.FirstName,
        LastName = u.LastName,
        PhoneNumber = u.PhoneNumber,
        Email = u.Email,
        RoleText = Ui.RoleText(u.Role)
    };

    private static ProfileVm FillReadOnly(ProfileVm vm, AppUser u)
    {
        vm.Email = u.Email;
        vm.RoleText = Ui.RoleText(u.Role);
        return vm;
    }
}
