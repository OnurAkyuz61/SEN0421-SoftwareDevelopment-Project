using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Oturumdaki kullanıcıyı tek bir yerden okur.
// AccountController giriş sonrası Session'a Id, rol damgası, jeton ve giriş
// zamanını yazar; kullanıcının kendisi ilk erişimde veritabanından okunup
// istek boyunca saklanır.
//
// Oturum dört şekilde sona erer:
//   1) Boşta kalma: AddSession → IdleTimeout (her istekte yenilenir)
//   2) Mutlak süre: girişten AbsoluteTimeoutHours sonra, işlem yapılsa bile
//   3) Hesap pasifleştirme: bir sonraki istekte Session temizlenir
//   4) Eşzamanlı oturum sınırı: yeni giriş eski jetonu DB'den düşürür
public class CurrentUser
{
    public const string SessionKey = SessionKeys.UserId;

    private readonly IHttpContextAccessor _http;
    private readonly AppDbContext _db;
    private readonly AuthSettings _settings;
    private readonly SessionRegistry _sessions;
    private AppUser? _cached;
    private bool _loaded;

    public CurrentUser(IHttpContextAccessor http, AppDbContext db, IOptions<AuthSettings> settings,
                       SessionRegistry sessions)
    {
        _http = http;
        _db = db;
        _settings = settings.Value;
        _sessions = sessions;
    }

    private ISession? Session => _http.HttpContext?.Session;

    public int? Id => Session?.GetInt32(SessionKey);

    // Bu istekte mutlak süre dolduğu için oturum kapatıldıysa true
    public bool ExpiredThisRequest { get; private set; }

    // Bu istekte hesap pasif bulunduğu için oturum kapatıldıysa true
    public bool DeactivatedThisRequest { get; private set; }

    // Bu istekte oturum jetonu geçersiz (başka yerden giriş / limit) ise true
    public bool ConcurrentReplacedThisRequest { get; private set; }

    // Bu istekte DB'deki rol, oturumda tutulan rolden farklıysa true;
    // yetkiler DB'den okunur, Session'daki rol damgası güncellenir.
    public bool RoleChangedThisRequest { get; private set; }

    // Rol değiştiyse oturumda görülen önceki rol (bildirim / yönlendirme için)
    public UserRole? PreviousRole { get; private set; }

    // Girişin yapıldığı an (UTC)
    public DateTime? SignedInAtUtc =>
        DateTime.TryParse(Session?.GetString(SessionKeys.IssuedAt), null,
                          System.Globalization.DateTimeStyles.RoundtripKind, out var t) ? t : null;

    // Pasifleştirilmiş / düşürülmüş oturumda Session temizlenir; null döner
    public AppUser? User
    {
        get
        {
            if (_loaded) return _cached;
            _loaded = true;

            var id = Id;
            if (id == null) return null;

            if (SignedInAtUtc is { } issued &&
                DateTime.UtcNow - issued > TimeSpan.FromHours(_settings.AbsoluteTimeoutHours))
            {
                Session!.Clear();
                ExpiredThisRequest = true;
                return null;
            }

            var user = _db.Users.AsNoTracking().FirstOrDefault(u => u.Id == id);
            if (user is null || !user.IsActive)
            {
                // Cookie / Session anahtarları da silinir; yalnızca User=null
                // bırakmak Id'nin dolu kalmasına ve yarım oturuma yol açardı
                Session!.Clear();
                if (user is { IsActive: false })
                    DeactivatedThisRequest = true;
                return null;
            }

            // Eşzamanlı oturum jetonu: yoksa veya DB'de yoksa bu oturum düşmüş demektir
            var session = Session;
            if (session is null) return null;
            var token = session.GetString(SessionKeys.SessionToken);
            if (!_sessions.IsOpen(user.Id, token ?? ""))
            {
                session.Clear();
                ConcurrentReplacedThisRequest = true;
                return null;
            }

            SyncSessionRole(user.Role);
            _cached = user;
            return _cached;
        }
    }

    // Oturumdaki rol damgasını DB ile hizalar. Yetki her zaman User.Role'dan
    // okunur; damga yalnızca değişikliği tespit etmek ve UI'ya haber vermek içindir.
    private void SyncSessionRole(UserRole dbRole)
    {
        var session = Session;
        if (session is null) return;

        var stamped = session.GetInt32(SessionKeys.Role);
        if (stamped is null)
        {
            // Eski oturum (damga yok): yaz, değişiklik sayma
            session.SetInt32(SessionKeys.Role, (int)dbRole);
            return;
        }

        var previous = (UserRole)stamped.Value;
        if (previous == dbRole) return;

        PreviousRole = previous;
        session.SetInt32(SessionKeys.Role, (int)dbRole);
        RoleChangedThisRequest = true;
    }

    public bool IsAuthenticated => User != null;
    public UserRole? Role => User?.Role;
    public bool IsStaff => Role is UserRole.Agent or UserRole.Admin;
    public string DisplayName => User?.FullName ?? "";

    // Periyodik şifre yenileme: süresi dolmuşsa true (PasswordMaxAgeDays ≤ 0 ise false)
    public bool IsPasswordExpired =>
        User is { } u && PasswordPolicy.IsExpired(u, _settings);

    // Avatar için baş harfler: "Rana Kurt" → "RK"
    public string Initials
    {
        get
        {
            var u = User;
            if (u == null) return "?";
            var a = u.FirstName.Length > 0 ? u.FirstName[0] : ' ';
            var b = u.LastName.Length > 0 ? u.LastName[0] : ' ';
            return $"{a}{b}".Trim().ToUpper(new System.Globalization.CultureInfo("tr-TR"));
        }
    }
}
