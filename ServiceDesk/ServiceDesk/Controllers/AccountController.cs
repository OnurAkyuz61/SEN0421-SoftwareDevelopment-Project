using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceDesk.Services.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// Giriş akışı: E-posta + şifre → OTP kanalı seçimi → 6 haneli kod → oturum
//
// Session'da iki ayrı durum tutulur:
//   • Bekleyen giriş (Otp.*): şifre doğru, kod henüz girilmedi
//   • Açık oturum (Auth.*):   kod doğrulandı, kullanıcı içeride
// Kod doğrulanınca bekleyen giriş tamamen silinir ve oturum sıfırdan açılır.
//
// Giriş sayfaları tarayıcı önbelleğine alınmaz; "geri" tuşuyla eski bir
// OTP ekranına dönülemez.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;
    private readonly CurrentUser _current;
    private readonly IWebHostEnvironment _env;
    private readonly LoginAuditService _audit;
    private readonly IEmailSender _email;
    private readonly AuthSettings _cfg;
    private readonly OtpDeliveryService _otp;
    private readonly AuditService _auditLog;

    public AccountController(AppDbContext db, IPasswordHasher<AppUser> hasher,
                             CurrentUser current, IWebHostEnvironment env,
                             LoginAuditService audit, IEmailSender email,
                             IOptions<AuthSettings> cfg, OtpDeliveryService otp,
                             AuditService auditLog)
    {
        _db = db;
        _hasher = hasher;
        _current = current;
        _env = env;
        _audit = audit;
        _email = email;
        _cfg = cfg.Value;
        _otp = otp;
        _auditLog = auditLog;
    }

    private ISession Session => HttpContext.Session;

    // ───── 1. ADIM: E-posta + şifre ──────────────────────────────────────

    [HttpGet]
    public IActionResult Login(string? returnUrl, string? reason)
    {
        // Zaten giriş yapılmışsa rolüne göre kendi sayfasına gönder.
        // Pasif hesap User null döner ve Session temizlenir.
        if (_current.User is { } signedIn) return RedirectAfterLogin(signedIn.Role, returnUrl);

        // timeout | expired | logout | deactivated
        ViewData["Reason"] = _current.DeactivatedThisRequest ? "deactivated" : reason;
        return View(new LoginVm { ReturnUrl = SafeReturnUrl(returnUrl) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        vm.ReturnUrl = SafeReturnUrl(vm.ReturnUrl);
        if (!ModelState.IsValid) return View(vm);

        vm.Email = vm.Email.Trim();

        // Art arda başarısız denemelerden sonra hesap geçici olarak kapanır
        if (await _audit.IsLockedOutAsync(vm.Email))
        {
            await _audit.RecordAsync(vm.Email, LoginResult.LockedOut);
            ModelState.AddModelError(string.Empty,
                $"Çok fazla başarısız deneme yapıldı. {LoginAuditService.Window.TotalMinutes:0} dakika sonra tekrar deneyin.");
            return View(vm);
        }

        // Pasif hesaplar da bulunur: şifre doğruysa ayrı sonuç + anlaşılır mesaj
        // yazılır; yanlışsa "hesap yok / şifre hatalı" ile karışmaz.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == vm.Email);

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

        // Şifre doğru ama hesap pasif: OTP'ye geçilmez, denetim izine ayrı yazılır
        if (!user.IsActive)
        {
            await _audit.RecordAsync(vm.Email, LoginResult.Inactive, user.Id);
            ModelState.AddModelError(string.Empty,
                "Hesabınız pasifleştirilmiş. Giriş yapamazsınız; yöneticinize başvurun.");
            return View(vm);
        }

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            await _audit.RecordAsync(vm.Email, LoginResult.NoPhone, user.Id);
            ModelState.AddModelError(string.Empty,
                "Bu hesapta doğrulama için telefon numarası tanımlı değil. Yöneticinize başvurun.");
            return View(vm);
        }

        // Önceki oturumdan kalan her şey silinir; bekleyen giriş temiz başlar
        Session.Clear();
        Session.SetInt32(SessionKeys.PendingUserId, user.Id);
        Session.SetString(SessionKeys.PendingPhone, user.PhoneNumber);
        SetTime(SessionKeys.PendingStartedAt, DateTime.UtcNow);
        if (vm.ReturnUrl != null) Session.SetString(SessionKeys.ReturnUrl, vm.ReturnUrl);

        return RedirectToAction(nameof(OtpChannel));
    }

    // ───── GELİŞTİRME KOLAYLIĞI ──────────────────────────────────────────
    // Test hesabına şifre ve OTP adımı olmadan girer. Giriş formu üzerinden
    // gitmediği için tarayıcının "bu şifre bir veri sızıntısında bulundu"
    // uyarısı da çıkmaz.
    //
    // ÜÇ KAT KİLİTLİ:
    //   1. [DevelopmentOnly]: ortam Development değilse ya da istek bu
    //      bilgisayardan gelmiyorsa action hiç çalışmadan 404 döner.
    //   2. Action içinde ortam bir kez daha kontrol edilir (filtre yanlışlıkla
    //      kaldırılırsa diye).
    //   3. Yalnızca seed hesapları için çalışır.
    // Her kullanım giriş denetim izine "Geliştirme girişi" olarak yazılır.
    // Canlıya çıkmadan önce bu action ve giriş sayfasındaki dev bloğu silinecek.
    [HttpPost]
    [DevelopmentOnly]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DevLogin(string? email, string? returnUrl)
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

        await _audit.RecordAsync(user.Email, LoginResult.DevLogin, user.Id);
        StartSession(user);

        TempData["Ok"] = $"Geliştirme girişi: {user.FullName} ({Ui.RoleText(user.Role)})";
        return RedirectAfterLogin(user.Role, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        Session.Clear();
        Response.Cookies.Delete(AuthSettings.CookieName);
        return RedirectToAction(nameof(Login), new { reason = "logout" });
    }

    // Sayfadaki "oturumu sürdür" düğmesi çağırır. Session'a erişmek boşta
    // kalma süresini sıfırlar; yanıt kalan süreyi döner.
    [HttpGet]
    [AuthorizeRole]
    public IActionResult KeepAlive() =>
        Json(new { ok = true, idleSeconds = _cfg.IdleTimeoutMinutes * 60 });

    // ───── 2. ADIM: Kodun gönderileceği kanal ────────────────────────────

    [HttpGet]
    public IActionResult OtpChannel()
    {
        if (!HasPendingLogin()) return RedirectToAction(nameof(Login));

        Enum.TryParse<OtpDeliveryChannel>(Session.GetString(SessionKeys.Channel), out var last);
        var enabled = _otp.EnabledChannels();
        if (!enabled.Contains(last) && enabled.Count > 0) last = enabled[0];

        ViewBag.EnabledChannels = enabled;
        return View(new OtpChannelVm
        {
            MaskedPhone = MaskPhone(Session.GetString(SessionKeys.PendingPhone)!),
            Channel = last
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.Otp)]
    public async Task<IActionResult> OtpChannel(OtpChannelVm vm)
    {
        if (!HasPendingLogin()) return RedirectToAction(nameof(Login));

        vm.MaskedPhone = MaskPhone(Session.GetString(SessionKeys.PendingPhone)!);
        ViewBag.EnabledChannels = _otp.EnabledChannels();

        if (ModelState.IsValid && !_otp.IsChannelEnabled(vm.Channel))
            ModelState.AddModelError(nameof(vm.Channel), "Bu kanal şu anda kullanılamıyor; başka bir kanal seçin.");
        if (!ModelState.IsValid) return View(vm);

        return await SendCodeAsync(vm.Channel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.Otp)]
    public async Task<IActionResult> ResendOtp()
    {
        if (!HasPendingLogin()) return RedirectToAction(nameof(Login));

        if (!Enum.TryParse<OtpDeliveryChannel>(Session.GetString(SessionKeys.Channel), out var channel))
            return RedirectToAction(nameof(OtpChannel));

        return await SendCodeAsync(channel);
    }

    // Kodu üretir, session'a yazar ve seçilen kanaldan gönderir
    // (OtpDeliveryService: SMS / WhatsApp, her deneme veritabanına yazılır).
    // Bekleme süresi ve toplam gönderim sınırı sunucu tarafında uygulanır;
    // butonu devre dışı bırakmak yalnızca görsel kolaylıktır.
    private async Task<IActionResult> SendCodeAsync(OtpDeliveryChannel channel)
    {
        var now = DateTime.UtcNow;
        var sendCount = Session.GetInt32(SessionKeys.SendCount) ?? 0;
        var pendingId = Session.GetInt32(SessionKeys.PendingUserId)!.Value;
        var email = await PendingEmailAsync(pendingId);

        if (sendCount >= _cfg.OtpMaxSends)
        {
            await _auditLog.WriteAsync(AuditAction.OtpSendLimitExceeded, pendingId, email,
                $"{sendCount} kod gönderildi, sınır {_cfg.OtpMaxSends}; bekleyen giriş iptal edildi.");
            ClearPendingLogin();
            TempData["Err"] = "Çok fazla kod istendi. Güvenliğiniz için lütfen yeniden giriş yapın.";
            return RedirectToAction(nameof(Login));
        }

        if (GetTime(SessionKeys.SentAt) is { } sentAt &&
            now - sentAt < TimeSpan.FromSeconds(_cfg.OtpResendCooldownSeconds) &&
            !string.IsNullOrEmpty(Session.GetString(SessionKeys.Code)))
        {
            var wait = (int)Math.Ceiling((sentAt.AddSeconds(_cfg.OtpResendCooldownSeconds) - now).TotalSeconds);
            TempData["OtpSentInfo"] = $"Yeni kod isteyebilmek için {wait} saniye bekleyin.";
            return RedirectToAction(nameof(OtpVerify));
        }

        // Kriptografik olarak güvenli 6 haneli kod (000000–999999)
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var phone = Session.GetString(SessionKeys.PendingPhone)!;
        var masked = MaskPhone(phone);

        // Gönderim sayılır (başarısız da olsa): sağlayıcıya sınırsız istek atılamasın
        Session.SetInt32(SessionKeys.SendCount, sendCount + 1);

        var delivery = await _otp.SendAsync(pendingId, email, phone, channel, code,
            _cfg.OtpLifetimeSeconds, sendCount + 1);

        if (delivery.Status is OtpDeliveryStatus.Failed or OtpDeliveryStatus.Disabled)
        {
            TempData["Err"] = channel == OtpDeliveryChannel.WhatsApp
                ? "Kod WhatsApp ile gönderilemedi. Lütfen SMS kanalını deneyin."
                : "Kod SMS ile gönderilemedi. Lütfen WhatsApp kanalını deneyin veya biraz sonra tekrar deneyin.";
            return RedirectToAction(nameof(OtpChannel));
        }

        Session.SetString(SessionKeys.Code, code);
        Session.SetString(SessionKeys.Channel, channel.ToString());
        SetTime(SessionKeys.ExpiresAt, now.AddSeconds(_cfg.OtpLifetimeSeconds));
        SetTime(SessionKeys.SentAt, now);
        Session.SetInt32(SessionKeys.Attempts, 0);

        // Kod yalnızca Development'ta ve bu bilgisayardan açıldığında ekranda da
        // gösterilir (şifre sıfırlama demo bağlantısı ve DevLogin ile aynı kural:
        // DevelopmentOnlyAttribute). Gönderim yine stub servisten geçer ve loglanır.
        if (DevelopmentOnlyAttribute.IsAllowed(HttpContext))
            TempData["OtpDemoCode"] = code;

        TempData["OtpSentInfo"] = channel == OtpDeliveryChannel.WhatsApp
            ? $"Kod WhatsApp ile {masked} numarasına gönderildi."
            : $"Kod SMS ile {masked} numarasına gönderildi.";

        return RedirectToAction(nameof(OtpVerify));
    }

    // ───── 3. ADIM: 6 haneli kod ─────────────────────────────────────────

    [HttpGet]
    public IActionResult OtpVerify()
    {
        if (!HasPendingLogin()) return RedirectToAction(nameof(Login));
        if (string.IsNullOrEmpty(Session.GetString(SessionKeys.Code))) return RedirectToAction(nameof(OtpChannel));

        var vm = new OtpVerifyVm();
        FillVerifyVm(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.Otp)]
    public async Task<IActionResult> OtpVerify(OtpVerifyVm vm)
    {
        if (!HasPendingLogin()) return RedirectToAction(nameof(Login));

        var expected = Session.GetString(SessionKeys.Code);
        if (string.IsNullOrEmpty(expected)) return RedirectToAction(nameof(OtpChannel));

        FillVerifyVm(vm);
        if (!ModelState.IsValid) return View(vm);

        var pendingId = Session.GetInt32(SessionKeys.PendingUserId);
        var email = await PendingEmailAsync(pendingId);
        Enum.TryParse<OtpDeliveryChannel>(Session.GetString(SessionKeys.Channel), out var usedChannel);

        // Süresi dolan kod bir daha kullanılamaz
        if (GetTime(SessionKeys.ExpiresAt) is not { } expires || DateTime.UtcNow > expires)
        {
            await _audit.RecordAsync(email, LoginResult.OtpExpired, pendingId);
            await _auditLog.WriteAsync(AuditAction.OtpExpired, pendingId, email,
                $"Süresi dolmuş kod girildi (kanal: {usedChannel}).");
            Session.Remove(SessionKeys.Code);
            vm.SecondsLeft = 0;
            ModelState.AddModelError(nameof(vm.Code), "Kodun süresi doldu. Yeni kod isteyin.");
            return View(vm);
        }

        var attempts = (Session.GetInt32(SessionKeys.Attempts) ?? 0) + 1;
        Session.SetInt32(SessionKeys.Attempts, attempts);

        // Sabit süreli karşılaştırma: yanıt süresinden kodun kaç hanesinin
        // doğru olduğu anlaşılamaz
        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(vm.Code), Encoding.ASCII.GetBytes(expected));

        if (!ok)
        {
            await _audit.RecordAsync(email, LoginResult.OtpFailed, pendingId);

            // Başarısız OTP denemesi denetim kaydına ayrıntısıyla yazılır:
            // kaçıncı deneme, hangi kanal, kalan hak (kodun kendisi yazılmaz)
            _auditLog.Queue(AuditAction.OtpFailed, pendingId, email,
                $"Hatalı OTP: deneme {attempts}/{_cfg.OtpMaxAttempts}, kanal {usedChannel}.");

            // Hesap genelindeki deneme sınırı (LoginAuditService)
            if (await _audit.IsLockedOutAsync(email))
            {
                _auditLog.Queue(AuditAction.AccountLocked, pendingId, email,
                    $"Art arda başarısız denemeler nedeniyle giriş {LoginAuditService.Window.TotalMinutes:0} dakika kapatıldı.");
                await _db.SaveChangesAsync();
                ClearPendingLogin();
                TempData["Err"] = "Çok fazla hatalı kod girildi. Lütfen daha sonra yeniden giriş yapın.";
                return RedirectToAction(nameof(Login));
            }

            // Bu koda özel deneme sınırı
            if (attempts >= _cfg.OtpMaxAttempts)
            {
                _auditLog.Queue(AuditAction.OtpAttemptsExceeded, pendingId, email,
                    $"Kod için {_cfg.OtpMaxAttempts} hatalı deneme; kod geçersiz kılındı.");
                await _db.SaveChangesAsync();
                Session.Remove(SessionKeys.Code);
                TempData["Err"] = "Bu kod için deneme hakkınız doldu. Yeni bir kod isteyin.";
                return RedirectToAction(nameof(OtpChannel));
            }

            await _db.SaveChangesAsync();
            vm.AttemptsLeft = _cfg.OtpMaxAttempts - attempts;
            ModelState.AddModelError(nameof(vm.Code),
                $"Kod hatalı. {vm.AttemptsLeft} deneme hakkınız kaldı.");
            return View(vm);
        }

        // OTP doğru olsa bile hesap bu sırada pasifleştirilmiş olabilir
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == pendingId);
        if (user is null)
        {
            ClearPendingLogin();
            TempData["Err"] = "Hesap bulunamadı.";
            return RedirectToAction(nameof(Login));
        }
        if (!user.IsActive)
        {
            await _audit.RecordAsync(user.Email, LoginResult.Inactive, user.Id);
            ClearPendingLogin();
            TempData["Err"] = "Hesabınız pasifleştirilmiş. Giriş yapamazsınız; yöneticinize başvurun.";
            return RedirectToAction(nameof(Login));
        }

        await _audit.RecordAsync(user.Email, LoginResult.Success, user.Id);

        var returnUrl = Session.GetString(SessionKeys.ReturnUrl);
        StartSession(user);

        return RedirectAfterLogin(user.Role, returnUrl);
    }

    // ───── ŞİFRE SIFIRLAMA ────────────────────────────────────────────────
    // Akış: e-posta iste → tek kullanımlık bağlantı → yeni şifre.
    // Hesabın var olup olmadığı yanıtta açıklanmaz (e-posta enumeration engeli).

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (_current.User is { } signedIn) return RedirectAfterLogin(signedIn.Role, null);
        return View(new ForgotPasswordVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVm vm)
    {
        if (_current.User is { } signedIn) return RedirectAfterLogin(signedIn.Role, null);
        if (!ModelState.IsValid) return View(vm);

        vm.Email = vm.Email.Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == vm.Email && u.IsActive);

        // Kullanıcı yoksa da aynı başarı sayfasına gideriz; süre farkı sızdırmaz
        string? demoLink = null;
        if (user is not null)
        {
            // Aynı hesabın eski (kullanılmamış) token'larını geçersiz kıl
            var eski = await _db.PasswordResetTokens
                .Where(t => t.UserId == user.Id && t.UsedAt == null)
                .ToListAsync();
            foreach (var t in eski) t.UsedAt = DateTime.UtcNow;

            var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            _db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = HashToken(raw),
                ExpiresAt = DateTime.UtcNow.AddHours(_cfg.PasswordResetHours),
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            var link = Url.Action(nameof(ResetPassword), "Account",
                new { token = raw }, Request.Scheme)!;

            await _email.SendAsync(
                user.Email,
                "ServiceDesk · Şifre sıfırlama",
                $"<p>Merhaba {user.FirstName},</p>" +
                $"<p>Şifrenizi sıfırlamak için <a href=\"{link}\">bu bağlantıya</a> tıklayın.</p>" +
                $"<p>Bağlantı {_cfg.PasswordResetHours} saat geçerlidir. İstek sizden gelmediyse bu e-postayı yok sayın.</p>",
                $"Merhaba {user.FirstName},\n\nŞifrenizi sıfırlamak için:\n{link}\n\n" +
                $"Bağlantı {_cfg.PasswordResetHours} saat geçerlidir.");

            // SMTP bağlanana kadar geliştirmede bağlantıyı ekranda göster
            if (_env.IsDevelopment()) demoLink = link;
        }

        return View("ForgotPasswordSent", new ForgotPasswordSentVm
        {
            Email = vm.Email,
            DemoResetLink = demoLink
        });
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string? token)
    {
        if (_current.User is { } signedIn) return RedirectAfterLogin(signedIn.Role, null);

        if (string.IsNullOrWhiteSpace(token) || await FindUsableTokenAsync(token) is null)
        {
            TempData["Err"] = "Şifre sıfırlama bağlantısı geçersiz veya süresi dolmuş. Lütfen yeniden isteyin.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordVm { Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordVm vm)
    {
        if (_current.User is { } signedIn) return RedirectAfterLogin(signedIn.Role, null);
        if (!ModelState.IsValid) return View(vm);

        var entry = await FindUsableTokenAsync(vm.Token);
        if (entry is null)
        {
            TempData["Err"] = "Şifre sıfırlama bağlantısı geçersiz veya süresi dolmuş. Lütfen yeniden isteyin.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == entry.UserId && u.IsActive);
        if (user is null)
        {
            TempData["Err"] = "Hesap bulunamadı veya pasif durumda.";
            return RedirectToAction(nameof(Login));
        }

        user.PasswordHash = _hasher.HashPassword(user, vm.Password);
        entry.UsedAt = DateTime.UtcNow;

        // Diğer açık sıfırlama token'larını da kapat
        var diger = await _db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.Id != entry.Id)
            .ToListAsync();
        foreach (var t in diger) t.UsedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Şifreniz güncellendi. Yeni şifrenizle giriş yapabilirsiniz.";
        return RedirectToAction(nameof(Login));
    }

    private async Task<PasswordResetToken?> FindUsableTokenAsync(string rawToken)
    {
        var hash = HashToken(rawToken);
        var entry = await _db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null);
        if (entry is null) return null;
        return DateTime.UtcNow <= entry.ExpiresAt ? entry : null;
    }

    private static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    // ───── Yardımcılar ───────────────────────────────────────────────────

    private async Task<string> PendingEmailAsync(int? pendingId) =>
        await _db.Users.Where(u => u.Id == pendingId).Select(u => u.Email).FirstOrDefaultAsync() ?? "";

    // Bekleyen giriş var mı? Süresi dolduysa temizler ve kullanıcıya söyler.
    private bool HasPendingLogin()
    {
        if (Session.GetInt32(SessionKeys.PendingUserId) is null ||
            string.IsNullOrEmpty(Session.GetString(SessionKeys.PendingPhone)))
            return false;

        if (GetTime(SessionKeys.PendingStartedAt) is not { } started ||
            DateTime.UtcNow - started > TimeSpan.FromMinutes(_cfg.PendingLoginMinutes))
        {
            ClearPendingLogin();
            TempData["Err"] = "Doğrulama adımı zaman aşımına uğradı. Lütfen yeniden giriş yapın.";
            return false;
        }
        return true;
    }

    private void FillVerifyVm(OtpVerifyVm vm)
    {
        var now = DateTime.UtcNow;
        Enum.TryParse<OtpDeliveryChannel>(Session.GetString(SessionKeys.Channel), out var channel);

        vm.MaskedPhone = MaskPhone(Session.GetString(SessionKeys.PendingPhone)!);
        vm.Channel = channel;
        vm.SecondsLeft = GetTime(SessionKeys.ExpiresAt) is { } exp ? Math.Max(0, (int)(exp - now).TotalSeconds) : 0;
        vm.ResendInSeconds = GetTime(SessionKeys.SentAt) is { } sent
            ? Math.Max(0, (int)Math.Ceiling((sent.AddSeconds(_cfg.OtpResendCooldownSeconds) - now).TotalSeconds))
            : 0;
        vm.SendsLeft = Math.Max(0, _cfg.OtpMaxSends - (Session.GetInt32(SessionKeys.SendCount) ?? 0));
        vm.AttemptsLeft = Math.Max(0, _cfg.OtpMaxAttempts - (Session.GetInt32(SessionKeys.Attempts) ?? 0));
    }

    // Oturumu sıfırdan açar: bekleyen girişe ait tüm anahtarlar silinir
    private void StartSession(AppUser user)
    {
        Session.Clear();
        Session.SetInt32(SessionKeys.UserId, user.Id);
        SetTime(SessionKeys.IssuedAt, DateTime.UtcNow);
    }

    private void ClearPendingLogin()
    {
        foreach (var key in new[]
                 {
                     SessionKeys.PendingUserId, SessionKeys.PendingPhone, SessionKeys.PendingStartedAt,
                     SessionKeys.ReturnUrl, SessionKeys.Code, SessionKeys.Channel, SessionKeys.ExpiresAt,
                     SessionKeys.SentAt, SessionKeys.SendCount, SessionKeys.Attempts
                 })
            Session.Remove(key);
    }

    // Yönetici ve destek yönetim paneline, son kullanıcı portala düşer.
    // Güvenli bir dönüş adresi varsa oraya gidilir.
    private IActionResult RedirectAfterLogin(UserRole role, string? returnUrl)
    {
        var safe = SafeReturnUrl(returnUrl);
        if (safe != null) return LocalRedirect(safe);

        return role == UserRole.User
            ? RedirectToAction("Index", "Portal")
            : RedirectToAction("Index", "Dashboard");
    }

    // Yalnızca bu sitedeki adreslere dönülür (açık yönlendirme engeli);
    // giriş sayfalarının kendisine dönülmez.
    private string? SafeReturnUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && Url.IsLocalUrl(url) &&
        !url.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)
            ? url
            : null;

    private void SetTime(string key, DateTime utc) =>
        Session.SetString(key, utc.ToString("O", CultureInfo.InvariantCulture));

    private DateTime? GetTime(string key) =>
        DateTime.TryParse(Session.GetString(key), CultureInfo.InvariantCulture,
                          DateTimeStyles.RoundtripKind, out var t) ? t : null;

    private static string MaskPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 7) return "***";
        return $"{digits[..4]} *** ** {digits[^2..]}";
    }
}
