using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Oturumdaki kullanıcıyı tek bir yerden okur.
// AccountController giriş sonrası Session'a yalnızca Id ve giriş zamanını
// yazar; kullanıcının kendisi ilk erişimde veritabanından okunup istek
// boyunca saklanır.
//
// Oturum iki şekilde sona erer:
//   1) Boşta kalma: AddSession → IdleTimeout (her istekte yenilenir)
//   2) Mutlak süre: girişten AbsoluteTimeoutHours sonra, işlem yapılsa bile
public class CurrentUser
{
    public const string SessionKey = SessionKeys.UserId;

    private readonly IHttpContextAccessor _http;
    private readonly AppDbContext _db;
    private readonly AuthSettings _settings;
    private AppUser? _cached;
    private bool _loaded;

    public CurrentUser(IHttpContextAccessor http, AppDbContext db, IOptions<AuthSettings> settings)
    {
        _http = http;
        _db = db;
        _settings = settings.Value;
    }

    private ISession? Session => _http.HttpContext?.Session;

    public int? Id => Session?.GetInt32(SessionKey);

    // Bu istekte mutlak süre dolduğu için oturum kapatıldıysa true
    public bool ExpiredThisRequest { get; private set; }

    // Girişin yapıldığı an (UTC)
    public DateTime? SignedInAtUtc =>
        DateTime.TryParse(Session?.GetString(SessionKeys.IssuedAt), null,
                          System.Globalization.DateTimeStyles.RoundtripKind, out var t) ? t : null;

    // Pasifleştirilmiş kullanıcı oturumu açık kalsa bile null döner
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

            _cached = _db.Users.AsNoTracking().FirstOrDefault(u => u.Id == id && u.IsActive);
            return _cached;
        }
    }

    public bool IsAuthenticated => User != null;
    public UserRole? Role => User?.Role;
    public bool IsStaff => Role is UserRole.Agent or UserRole.Admin;
    public string DisplayName => User?.FullName ?? "";

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
