using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Messaging;
using ServiceDesk.Services.Notifications;

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
    private readonly NotificationService _notify;
    private readonly DepartmentTransferReassignment _deptTransfer;
    private readonly InactiveUserTicketRelease _ticketRelease;
    private readonly UserAnonymization _anonymize;
    private readonly IEmailSender _email;
    private readonly AuthSettings _auth;
    private readonly IWebHostEnvironment _env;

    public UsersController(AppDbContext db, CurrentUser current,
                           SessionRegistry sessions, PasswordHistoryService passwordHistory,
                           AuditService audit, NotificationService notify,
                           DepartmentTransferReassignment deptTransfer,
                           InactiveUserTicketRelease ticketRelease,
                           UserAnonymization anonymize,
                           IEmailSender email, IOptions<AuthSettings> auth, IWebHostEnvironment env)
    {
        _db = db;
        _current = current;
        _sessions = sessions;
        _passwordHistory = passwordHistory;
        _audit = audit;
        _notify = notify;
        _deptTransfer = deptTransfer;
        _ticketRelease = ticketRelease;
        _anonymize = anonymize;
        _email = email;
        _auth = auth.Value;
        _env = env;
    }

    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> Index(string? q, UserRole? role, bool? active,
                                           bool missingPhone = false, int page = 1)
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
        if (missingPhone)
            query = query.Where(u => u.IsActive && (u.PhoneNumber == null || u.PhoneNumber == ""));

        ViewBag.Q = q;
        ViewBag.Role = role;
        ViewBag.Active = active;
        ViewBag.MissingPhone = missingPhone;
        ViewBag.PassiveCount = await _db.Users.CountAsync(u => !u.IsActive);
        ViewBag.MissingPhoneCount = await _db.Users.CountAsync(u =>
            u.IsActive && (u.PhoneNumber == null || u.PhoneNumber == ""));

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
        var query = FilteredLoginAttempts(q, result, failedOnly, from, to);

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

    // Aynı filtrelerle CSV (Excel uyumlu). En fazla MaxRows satır.
    [HttpGet]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> ExportLoginAttemptsCsv(string? q, LoginResult? result, bool failedOnly = false,
                                                            DateTime? from = null, DateTime? to = null)
    {
        var rows = await FilteredLoginAttempts(q, result, failedOnly, from, to)
            .OrderByDescending(l => l.AttemptedAt)
            .Take(LoginAttemptsCsv.MaxRows)
            .ToListAsync();

        var csv = LoginAttemptsCsv.Build(rows.Select(LoginAttemptsCsv.From));
        var stamp = AppTime.ToLocal(AppTime.UtcNow).ToString("yyyyMMdd-HHmm");
        return File(TicketListCsv.ToUtf8BomBytes(csv), "text/csv; charset=utf-8",
            $"giris-denemeleri-{stamp}.csv");
    }

    private IQueryable<LoginAttempt> FilteredLoginAttempts(string? q, LoginResult? result, bool failedOnly,
                                                           DateTime? from, DateTime? to)
    {
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

        return query;
    }

    public record LoginTargetRow(string Email, int Count);

    // ───── CSV toplu içe aktarma ─────────────────────────────────────────

    [HttpGet]
    [AuthorizeRole(UserRole.Admin)]
    public IActionResult ImportCsv() => View();

    [HttpGet]
    [AuthorizeRole(UserRole.Admin)]
    public IActionResult ImportCsvTemplate()
    {
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(UserCsvImport.TemplateCsv())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", "kullanici-sablon.csv");
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Admin)]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> ImportCsv(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("file", "CSV dosyası seçin.");
            return View();
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            && file.ContentType is not ("text/csv" or "application/vnd.ms-excel" or "text/plain"))
        {
            ModelState.AddModelError("file", "Yalnızca .csv dosyası yükleyin.");
            return View();
        }

        string content;
        await using (var stream = file.OpenReadStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            content = await reader.ReadToEndAsync();

        var parsed = UserCsvImport.Parse(content);
        var result = new UserCsvImportResultVm { Errors = parsed.Errors.ToList() };

        if (parsed.Rows.Count == 0)
        {
            if (result.Errors.Count == 0)
                result.Errors.Add((0, "Aktarılacak geçerli satır yok."));
            return View("ImportCsvResult", result);
        }

        var me = _current.User!;
        var existingEmails = (await _db.Users.AsNoTracking().Select(u => u.Email).ToListAsync())
            .Select(e => e.Trim().ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var existingPhones = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in await _db.Users.AsNoTracking()
                     .Where(u => u.PhoneNumber != null).Select(u => u.PhoneNumber!).ToListAsync())
        {
            var n = PhoneRules.Normalize(raw);
            if (n != null) existingPhones.Add(n);
        }

        var departments = await _db.Departments.AsNoTracking().Where(d => d.IsActive).ToListAsync();
        var teams = await _db.Teams.AsNoTracking().Where(t => t.IsActive).ToListAsync();

        foreach (var row in parsed.Rows)
        {
            if (existingEmails.Contains(row.Email))
            {
                result.SkippedExisting++;
                continue;
            }

            if (row.Phone != null && existingPhones.Contains(row.Phone))
            {
                result.Errors.Add((row.Line, $"Telefon başka bir hesapta kayıtlı: {row.Phone}"));
                continue;
            }

            int? deptId = null;
            if (row.DepartmentName != null)
            {
                var d = departments.FirstOrDefault(x => CatalogRules.SameName(x.Name, row.DepartmentName));
                if (d == null)
                {
                    result.Errors.Add((row.Line, $"Birim bulunamadı: {row.DepartmentName}"));
                    continue;
                }
                deptId = d.Id;
            }

            int? teamId = null;
            int? teamDeptId = null;
            if (row.TeamName != null)
            {
                var t = teams.FirstOrDefault(x => CatalogRules.SameName(x.Name, row.TeamName));
                if (t == null)
                {
                    result.Errors.Add((row.Line, $"Ekip bulunamadı: {row.TeamName}"));
                    continue;
                }
                teamId = t.Id;
                teamDeptId = t.DepartmentId;
            }

            var org = OrganizationRules.Resolve(deptId, teamId, teamId != null, teamDeptId);
            if (org.Error != null)
            {
                result.Errors.Add((row.Line, org.Error));
                continue;
            }

            var tempPassword = row.Password ?? UserCsvImport.GenerateTempPassword();
            var user = new AppUser
            {
                FirstName = row.FirstName,
                LastName = row.LastName,
                Email = row.Email,
                PhoneNumber = row.Phone,
                Role = row.Role,
                DepartmentId = org.DepartmentId,
                TeamId = org.TeamId,
                IsActive = true,
                CreatedAt = AppTime.UtcNow
            };
            _db.Users.Add(user);
            await _passwordHistory.SetPasswordAsync(user, tempPassword, requireChangeOnNextLogin: true);

            existingEmails.Add(row.Email);
            if (row.Phone != null) existingPhones.Add(row.Phone);

            result.Created++;
            result.CreatedUsers.Add((row.Email, $"{row.FirstName} {row.LastName}",
                row.Password == null ? tempPassword : null));
        }

        if (result.Created > 0)
        {
            _audit.Queue(AuditAction.UsersCsvImported, me.Id, me.Email,
                detail: $"CSV içe aktarma: {result.Created} oluşturuldu, {result.SkippedExisting} atlandı (mevcut), {result.Errors.Count} hata");
            await _db.SaveChangesAsync();
        }

        return View("ImportCsvResult", result);
    }

    // E-posta daveti: hesap henüz oluşmaz; davetli bağlantıdan şifre + telefon belirler.
    [HttpGet]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Invite()
    {
        await LoadFormListsAsync();
        return View(new UserInviteVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Invite(UserInviteVm vm)
    {
        var me = _current.User!;
        vm.Email = vm.Email.Trim().ToLowerInvariant();
        vm.FirstName = vm.FirstName.Trim();
        vm.LastName = vm.LastName.Trim();

        if (await _db.Users.AnyAsync(u => u.Email == vm.Email))
            ModelState.AddModelError(nameof(vm.Email), "Bu e-posta zaten kayıtlı; davet yerine hesabı düzenleyin.");

        int? teamDepartmentId = null;
        var teamFound = false;
        if (vm.TeamId != null)
        {
            var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == vm.TeamId);
            teamFound = team != null;
            teamDepartmentId = team?.DepartmentId;
        }
        var org = OrganizationRules.Resolve(vm.DepartmentId, vm.TeamId, teamFound, teamDepartmentId);
        vm.DepartmentId = org.DepartmentId;
        vm.TeamId = org.TeamId;
        if (org.Error != null)
            ModelState.AddModelError(nameof(vm.TeamId), org.Error);

        if (vm.DepartmentId != null && !await _db.Departments.AnyAsync(d => d.Id == vm.DepartmentId))
            ModelState.AddModelError(nameof(vm.DepartmentId), "Geçerli bir birim seçin");

        if (!ModelState.IsValid)
        {
            await LoadFormListsAsync();
            return View(vm);
        }

        // Aynı e-postadaki açık davetleri geçersiz kıl
        var eski = await _db.UserInvites
            .Where(i => i.Email == vm.Email && i.UsedAt == null)
            .ToListAsync();
        foreach (var i in eski) i.UsedAt = AppTime.UtcNow;

        var raw = CreateInviteToken();
        var invite = new UserInvite
        {
            Email = vm.Email,
            FirstName = vm.FirstName,
            LastName = vm.LastName,
            Role = vm.Role,
            DepartmentId = vm.DepartmentId,
            TeamId = vm.TeamId,
            TokenHash = HashInviteToken(raw),
            ExpiresAt = AppTime.UtcNow.AddHours(Math.Max(1, _auth.InviteHours)),
            CreatedAt = AppTime.UtcNow,
            InvitedById = me.Id
        };
        _db.UserInvites.Add(invite);

        _audit.Queue(AuditAction.UserInvited, me.Id, me.Email,
            detail: $"Davet: {vm.FirstName} {vm.LastName} <{vm.Email}> · {Ui.RoleText(vm.Role)}");

        await _db.SaveChangesAsync();

        var link = Url.Action("AcceptInvite", "Account", new { token = raw }, Request.Scheme)!;
        await _email.SendAsync(
            vm.Email,
            "ServiceDesk · Hesap daveti",
            $"<p>Merhaba {vm.FirstName},</p>" +
            $"<p>{me.FullName} sizi ServiceDesk'e <strong>{Ui.RoleText(vm.Role)}</strong> olarak davet etti.</p>" +
            $"<p>Hesabınızı tamamlamak için <a href=\"{link}\">bu bağlantıya</a> tıklayın.</p>" +
            $"<p>Bağlantı {_auth.InviteHours} saat geçerlidir.</p>",
            $"Merhaba {vm.FirstName},\n\n{me.FullName} sizi ServiceDesk'e {Ui.RoleText(vm.Role)} olarak davet etti.\n\n" +
            $"Hesabınızı tamamlamak için:\n{link}\n\nBağlantı {_auth.InviteHours} saat geçerlidir.");

        TempData["Ok"] = $"{vm.Email} adresine davet gönderildi.";
        if (_env.IsDevelopment())
            TempData["Ok"] += $" Demo bağlantı: {link}";

        return RedirectToAction(nameof(Index));
    }

    private static string CreateInviteToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string HashInviteToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    // Oluşturma ve düzenleme aynı ekran: id boşsa yeni kayıt
    [HttpGet]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Edit(int? id)
    {
        await LoadFormListsAsync();

        if (id == null) return View(new UserFormVm());

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (UserAnonymizationPolicy.IsAnonymized(user))
            ViewBag.Anonymized = true;

        var vm = UserFormVm.From(user);
        vm.OpenTicketCount = await _db.Tickets.CountAsync(t =>
            t.AssigneeId == user.Id &&
            (t.Status == TicketStatus.New
             || t.Status == TicketStatus.InProgress
             || t.Status == TicketStatus.WaitingForCustomer));

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
            if (UserAnonymizationPolicy.IsAnonymized(user))
            {
                TempData["Err"] = "Anonimleştirilmiş hesap düzenlenemez (KVKK).";
                return RedirectToAction(nameof(Index));
            }
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

        // Birim ↔ ekip tutarlılığı: birim değişince eski ekip başka birime
        // ait kalamaz (OrganizationRules). Ekip seçili / birim boşsa birim
        // ekipten doldurulur.
        int? teamDepartmentId = null;
        var teamFound = false;
        if (vm.TeamId != null)
        {
            var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == vm.TeamId);
            teamFound = team != null;
            teamDepartmentId = team?.DepartmentId;
        }

        var org = OrganizationRules.Resolve(vm.DepartmentId, vm.TeamId, teamFound, teamDepartmentId);
        vm.DepartmentId = org.DepartmentId;
        vm.TeamId = org.TeamId;
        if (org.Error != null)
            ModelState.AddModelError(nameof(vm.TeamId), org.Error);

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
        var oncekiTelefonYok = user != null && string.IsNullOrWhiteSpace(user.PhoneNumber);
        var oncekiAktif = user?.IsActive ?? false;
        var oncekiEkipId = user?.TeamId;
        var oncekiAlanlar = user == null ? null : UserFieldChangeAudit.Capture(user);
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
        var sifreBelirlendi = !string.IsNullOrEmpty(vm.Password);
        if (sifreBelirlendi)
            await _passwordHistory.SetPasswordAsync(user, vm.Password!, requireChangeOnNextLogin: true);

        if (!yeniKayit && oncekiAlanlar != null)
        {
            var degisen = UserFieldChangeAudit.Diff(oncekiAlanlar, UserFieldChangeAudit.Capture(user), sifreBelirlendi);
            if (UserFieldChangeAudit.FormatDetail(user.Id, user.FullName, degisen) is { } detay)
                _audit.Queue(AuditAction.UserFieldsChanged, me.Id, me.Email, detail: detay);
        }

        // Pasifleştirme: açık talepler atamasız bırakılır (ekip kuyruğu kalır).
        // Ekip değişimi: pasif değilse eski kuyrukta yeniden atanır.
        InactiveUserTicketRelease.Result? release = null;
        DepartmentTransferReassignment.Result? transfer = null;
        if (!yeniKayit && InactiveUserTicketReleasePolicy.ShouldRelease(oncekiAktif, user.IsActive))
        {
            release = await _ticketRelease.ApplyAsync(user.Id, me.Id, user.FullName);
        }
        else if (!yeniKayit && DepartmentTransferRules.RequiresReassignment(oncekiEkipId, user.TeamId))
        {
            transfer = await _deptTransfer.ApplyAsync(
                user.Id, oncekiEkipId, user.TeamId, me.Id, user.FullName);
        }

        await _db.SaveChangesAsync();

        // Yeni hesap Id'si SaveChanges sonrası belli olur
        if (yeniKayit)
        {
            _audit.Queue(AuditAction.UserFieldsChanged, me.Id, me.Email,
                detail: $"Yeni hesap #{user.Id}: {user.FullName} ({user.Email}), rol {Ui.RoleText(user.Role)}");
            await _db.SaveChangesAsync();
        }

        if (transfer is { TicketIds.Count: > 0 })
            _notify.SignalDelivery();

        if (pasiflestirildi)
            await _sessions.CloseAllAsync(user.Id);

        // Aktif + telefonsuz: OTP giriş yapılamaz → yöneticilere uyarı
        // (yalnızca yeni / telefon silindi / aktifleştirildi durumlarında; spam olmasın)
        var simdiTelefonYok = string.IsNullOrWhiteSpace(user.PhoneNumber);
        if (user.IsActive && simdiTelefonYok &&
            (yeniKayit || !oncekiTelefonYok || (!oncekiAktif && user.IsActive)))
        {
            await _notify.QueueToRoleAsync(UserRole.Admin, NotificationType.MissingPhone,
                "Telefonu tanımlı olmayan hesap",
                $"{user.FullName} ({user.Email}) aktif ama telefon numarası yok; OTP ile giriş yapamaz.",
                templateKey: EmailTemplates.MissingPhone);
            _notify.SignalDelivery();
            await _db.SaveChangesAsync();
        }

        TempData["Ok"] = yeniKayit
            ? $"{user.FullName} ({Ui.RoleText(user.Role)}) hesabı oluşturuldu. İlk girişte şifre değiştirmesi istenecek."
            : pasiflestirildi
                ? $"{user.FullName} hesabı pasifleştirildi. Talep geçmişi korundu, açık oturumları kapatıldı." +
                  (release is { Released: > 0 }
                      ? $" {release.Released} açık talep atamasız bırakıldı."
                      : "")
                : transfer is { TicketIds.Count: > 0 }
                    ? $"{user.FullName} hesabı güncellendi. Açık talepler: {transfer.Reassigned} yeniden atandı" +
                      (transfer.Unassigned > 0 ? $", {transfer.Unassigned} atamasız bırakıldı." : ".")
                    : rolDegisti
                        ? $"{user.FullName} rolü {Ui.RoleText(user.Role)} olarak güncellendi. Açık oturumundaki yetkiler bir sonraki istekte yenilenir."
                        : sifreBelirlendi
                            ? $"{user.FullName} hesabı güncellendi. Yeni şifre geçici; bir sonraki girişte değiştirmesi istenecek."
                            : $"{user.FullName} hesabı güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    // KVKK: kişisel verileri scrub eder, hesabı pasifleştirir; Id ve talep
    // geçmişi FK ile kalır. Geri alınamaz.
    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizeRole(UserRole.Admin)]
    public async Task<IActionResult> Anonymize(int id, string? returnUrl)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (user.Id == _current.User!.Id)
        {
            TempData["Err"] = "Kendi hesabınızı anonimleştiremezsiniz.";
        }
        else if (UserAnonymizationPolicy.IsAnonymized(user))
        {
            TempData["Err"] = "Bu hesap zaten anonimleştirilmiş.";
        }
        else if (user is { Role: UserRole.Admin, IsActive: true } &&
                 !await OtherActiveAdminExistsAsync(user.Id))
        {
            TempData["Err"] = "Sistemdeki tek aktif yönetici anonimleştirilemez.";
        }
        else if (await LedTeamNamesAsync(user.Id) is { Count: > 0 } ekipler)
        {
            TempData["Err"] = $"{user.FullName} {string.Join(", ", ekipler)} ekibinin lideri; " +
                              "önce Katalog ekranından liderliği devredin.";
        }
        else
        {
            var ad = user.FullName;
            var result = await _anonymize.ApplyAsync(user, _current.User.Id);
            await _db.SaveChangesAsync();

            TempData["Ok"] = result.AlreadyAnonymized
                ? "Hesap zaten anonimleştirilmişti."
                : $"{ad} anonimleştirildi (KVKK). Kişisel veriler silindi; talep geçmişi korundu." +
                  (result.ReleasedTickets > 0
                      ? $" {result.ReleasedTickets} açık talep atamasız bırakıldı."
                      : "");
        }

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
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
        else if (isActive && UserAnonymizationPolicy.IsAnonymized(user))
        {
            TempData["Err"] = "Anonimleştirilmiş hesap yeniden aktifleştirilemez (KVKK).";
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
            var oncekiAktif = user.IsActive;
            var oncekiAlanlar = UserFieldChangeAudit.Capture(user);
            user.IsActive = isActive;

            InactiveUserTicketRelease.Result? release = null;
            if (InactiveUserTicketReleasePolicy.ShouldRelease(oncekiAktif, isActive))
                release = await _ticketRelease.ApplyAsync(user.Id, _current.User!.Id, user.FullName);

            var degisen = UserFieldChangeAudit.Diff(oncekiAlanlar, UserFieldChangeAudit.Capture(user));
            if (UserFieldChangeAudit.FormatDetail(user.Id, user.FullName, degisen) is { } detay)
                _audit.Queue(AuditAction.UserFieldsChanged, _current.User!.Id, _current.User.Email, detail: detay);

            await _db.SaveChangesAsync();

            if (!isActive)
                await _sessions.CloseAllAsync(user.Id);

            TempData["Ok"] = isActive
                ? $"{user.FullName} hesabı yeniden aktifleştirildi."
                : $"{user.FullName} hesabı pasifleştirildi. Talep geçmişi korundu; açık oturumları kapatıldı." +
                  (release is { Released: > 0 }
                      ? $" {release.Released} açık talep atamasız bırakıldı."
                      : "");
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
