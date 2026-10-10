using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Controllers;

public class AccountController : Controller
{
    private const string SessUserId = "Otp.UserId";
    private const string SessPhone = "Otp.Phone";
    private const string SessCode = "Otp.Code";
    private const string SessChannel = "Otp.Channel";
    private const string SessExpires = "Otp.Expires";
    private const string SessAuthed = "Auth.UserId";

    private readonly AppDbContext _db;

    public AccountController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Login() => View(new LoginVm());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == vm.Email && u.Password == vm.Password);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "E-posta veya şifre hatalı.");
            return View(vm);
        }

        HttpContext.Session.SetInt32(SessUserId, user.Id);
        HttpContext.Session.SetString(SessPhone, user.PhoneNumber);
        HttpContext.Session.Remove(SessCode);
        HttpContext.Session.Remove(SessChannel);
        HttpContext.Session.Remove(SessExpires);

        return RedirectToAction(nameof(OtpChannel));
    }

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
    public IActionResult OtpVerify(OtpVerifyVm vm)
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
            ModelState.AddModelError(nameof(vm.Code), "Kodun süresi doldu. Yeni kod isteyin.");
            return View(vm);
        }

        var expected = HttpContext.Session.GetString(SessCode);
        if (!string.Equals(vm.Code, expected, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(vm.Code), "Kod hatalı. Tekrar deneyin.");
            return View(vm);
        }

        var userId = HttpContext.Session.GetInt32(SessUserId)!.Value;
        HttpContext.Session.SetInt32(SessAuthed, userId);
        ClearOtpSession();

        return RedirectToAction("Index", "Dashboard");
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
