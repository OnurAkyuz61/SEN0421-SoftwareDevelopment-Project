using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

public class AccountController : Controller
{
    private const string SessUserId = "Otp.UserId";
    private const string SessPhone = "Otp.Phone";
    private const string SessCode = "Otp.Code";
    private const string SessChannel = "Otp.Channel";
    private const string SessExpires = "Otp.Expires";
    private const string SessAuthed = CurrentUser.SessionKey;

    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;
    private readonly CurrentUser _current;
    private readonly IWebHostEnvironment _env;
    private readonly LoginAuditService _audit;

    public AccountController(AppDbContext db, IPasswordHasher<AppUser> hasher,
                             CurrentUser current, IWebHostEnvironment env,
                             LoginAuditService audit)
    {
        _db = db;
        _hasher = hasher;
        _current = current;
        _env = env;
        _audit = audit;
    }

    [HttpGet]
    public IActionResult Login()
    {
        // Zaten giriş yapılmışsa rolüne göre kendi sayfasına gönder
        if (_current.User is { } signedIn) return RedirectToHome(signedIn.Role);
        return View(new LoginVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        // Art arda başarısız denemelerden sonra hesap geçici olarak kapanır
        if (await _audit.IsLockedOutAsync(vm.Email))
        {
            await _audit.RecordAsync(vm.Email, LoginResult.LockedOut);
            ModelState.AddModelError(string.Empty,
                $"Çok fazla başarısız deneme yapıldı. {LoginAuditService.Window.TotalMinutes:0} dakika sonra tekrar deneyin.");
            return View(vm);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == vm.Email && u.IsActive);

        // Kullanıcı bulunamasa da şifre doğrulaması yapılır ki yanıt süresinden
        // hesabın var olup olmadığı anlaşılmasın
        var verified = user is not null &&
            _hasher.VerifyHashedPassword(user, user.PasswordHash, vm.Password)
                != PasswordVerificationResult.Failed;

        if (user is null || !verified)
        {
            await _audit.RecordAsync(vm.Email,
                user is null ? LoginResult.UnknownEmail : LoginResult.WrongPassword, user?.Id);

            var kalan = _audit.RemainingAttempts(await _audit.RecentFailuresAsync(vm.Email));
            ModelState.AddModelError(string.Empty, kalan <= 2
                ? $"E-posta veya şifre hatalı. Kalan deneme hakkı: {kalan}."
                : "E-posta veya şifre hatalı.");
            return View(vm);
        }

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            await _audit.RecordAsync(vm.Email, LoginResult.NoPhone, user.Id);
            ModelState.AddModelError(string.Empty,
                "Bu hesapta doğrulama için telefon numarası tanımlı değil. Yöneticinize başvurun.");
            return View(vm);
        }

        HttpContext.Session.SetInt32(SessUserId, user.Id);
        HttpContext.Session.SetString(SessPhone, user.PhoneNumber);
        HttpContext.Session.Remove(SessCode);
        HttpContext.Session.Remove(SessChannel);
        HttpContext.Session.Remove(SessExpires);

        return RedirectToAction(nameof(OtpChannel));
    }

    // ───── GELİŞTİRME KOLAYLIĞI ──────────────────────────────────────────
    // Test hesabına şifre ve OTP adımı olmadan girer. Giriş formu üzerinden
    // gitmediği için tarayıcının "bu şifre bir veri sızıntısında bulundu"
    // uyarısı da çıkmaz.
    //
    // İKİ KAT KİLİTLİ: yalnızca Development ortamında ve yalnızca seed
    // hesapları için çalışır. Canlıya çıkmadan önce bu action ve giriş
    // sayfasındaki dev bloğu silinecek.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DevLogin(string? email)
    {
        if (!_env.IsDevelopment()) return NotFound();

        string[] izinli =
        {
            "admin@servicedesk.local",
            "agent@servicedesk.local",
            "user@servicedesk.local"
        };
        if (email == null || !izinli.Contains(email)) return NotFound();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
        if (user == null) return NotFound();

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessAuthed, user.Id);

        TempData["Ok"] = $"Geliştirme girişi: {user.FullName} ({Ui.RoleText(user.Role)})";
        return RedirectToHome(user.Role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    // Yönetici ve destek yönetim paneline, son kullanıcı portala düşer
    private IActionResult RedirectToHome(UserRole role) =>
        role == UserRole.User
            ? RedirectToAction("Index", "Portal")
            : RedirectToAction("Index", "Dashboard");

    [HttpGet]
    public IActionResult OtpChannel()
    {
        if (!HasPendingLogin()) return RedirectToAction(nameof(Login));

        return View(new OtpChannelVm
        {
            MaskedPhone = MaskPhone(HttpContext.Session.GetString(SessPhone)!)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult OtpChannel(OtpChannelVm vm)
    {
        if (!HasPendingLogin()) return RedirectToAction(nameof(Login));

        vm.MaskedPhone = MaskPhone(HttpContext.Session.GetString(SessPhone)!);
        if (!ModelState.IsValid) return View(vm);

        var code = Random.Shared.Next(100000, 999999).ToString();

        HttpContext.Session.SetString(SessCode, code);
        HttpContext.Session.SetString(SessChannel, vm.Channel.ToString());
        HttpContext.Session.SetString(SessExpires, DateTime.UtcNow.AddMinutes(5).ToString("O"));

        // Gerçek SMS / WhatsApp API buraya bağlanacak; şimdilik simülasyon
        TempData["OtpDemoCode"] = code;
        TempData["OtpSentInfo"] = vm.Channel == OtpDeliveryChannel.WhatsApp
            ? $"Doğrulama kodu WhatsApp ile {vm.MaskedPhone} numarasına gönderildi."
            : $"Doğrulama kodu SMS ile {vm.MaskedPhone} numarasına gönderildi.";

        return RedirectToAction(nameof(OtpVerify));
    }

    [HttpGet]
    public IActionResult OtpVerify()
    {
        if (!HasPendingLogin() || string.IsNullOrEmpty(HttpContext.Session.GetString(SessCode)))
            return RedirectToAction(nameof(OtpChannel));

        Enum.TryParse<OtpDeliveryChannel>(HttpContext.Session.GetString(SessChannel), out var channel);

        return View(new OtpVerifyVm
        {
            MaskedPhone = MaskPhone(HttpContext.Session.GetString(SessPhone)!),
            Channel = channel
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OtpVerify(OtpVerifyVm vm)
    {
        if (!HasPendingLogin() || string.IsNullOrEmpty(HttpContext.Session.GetString(SessCode)))
            return RedirectToAction(nameof(OtpChannel));

        Enum.TryParse<OtpDeliveryChannel>(HttpContext.Session.GetString(SessChannel), out var channel);
        vm.MaskedPhone = MaskPhone(HttpContext.Session.GetString(SessPhone)!);
        vm.Channel = channel;

        if (!ModelState.IsValid) return View(vm);

        var expiresRaw = HttpContext.Session.GetString(SessExpires);
        if (!DateTime.TryParse(expiresRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expires)
            || DateTime.UtcNow > expires)
        {
            await _audit.RecordAsync(_db.Users.Where(u => u.Id == HttpContext.Session.GetInt32(SessUserId))
                                        .Select(u => u.Email).FirstOrDefault() ?? "",
                                     LoginResult.OtpExpired, HttpContext.Session.GetInt32(SessUserId));
            ModelState.AddModelError(nameof(vm.Code), "Kodun süresi doldu. Yeni kod isteyin.");
            return View(vm);
        }

        var expected = HttpContext.Session.GetString(SessCode);
        if (!string.Equals(vm.Code, expected, StringComparison.Ordinal))
        {
            var bekleyenId = HttpContext.Session.GetInt32(SessUserId);
            var eposta = await _db.Users.Where(u => u.Id == bekleyenId).Select(u => u.Email).FirstOrDefaultAsync() ?? "";
            await _audit.RecordAsync(eposta, LoginResult.OtpFailed, bekleyenId);

            // Kod da sınırsız denenemez
            if (await _audit.IsLockedOutAsync(eposta))
            {
                ClearOtpSession();
                TempData["Err"] = "Çok fazla hatalı kod girildi. Lütfen yeniden giriş yapın.";
                return RedirectToAction(nameof(Login));
            }

            ModelState.AddModelError(nameof(vm.Code), "Kod hatalı. Tekrar deneyin.");
            return View(vm);
        }

        var userId = HttpContext.Session.GetInt32(SessUserId)!.Value;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user is null)
        {
            ClearOtpSession();
            ModelState.AddModelError(string.Empty, "Hesap bulunamadı veya pasif durumda.");
            return RedirectToAction(nameof(Login));
        }

        await _audit.RecordAsync(user.Email, LoginResult.Success, user.Id);

        HttpContext.Session.SetInt32(SessAuthed, user.Id);
        ClearOtpSession();

        return RedirectToHome(user.Role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ResendOtp()
    {
        if (!HasPendingLogin()) return RedirectToAction(nameof(Login));

        var channelRaw = HttpContext.Session.GetString(SessChannel);
        if (string.IsNullOrEmpty(channelRaw)
            || !Enum.TryParse<OtpDeliveryChannel>(channelRaw, out var channel))
            return RedirectToAction(nameof(OtpChannel));

        return OtpChannel(new OtpChannelVm { Channel = channel });
    }

    private bool HasPendingLogin() =>
        HttpContext.Session.GetInt32(SessUserId).HasValue
        && !string.IsNullOrEmpty(HttpContext.Session.GetString(SessPhone));

    private void ClearOtpSession()
    {
        HttpContext.Session.Remove(SessCode);
        HttpContext.Session.Remove(SessChannel);
        HttpContext.Session.Remove(SessExpires);
        HttpContext.Session.Remove(SessUserId);
        HttpContext.Session.Remove(SessPhone);
    }

    private static string MaskPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 7) return "***";
        return $"{digits[..4]} *** ** {digits[^2..]}";
    }
}
