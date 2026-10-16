using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Oturumdaki kullanıcıyı tek bir yerden okur.
// AccountController giriş sonrası Session'a yalnızca Id yazar; kullanıcının
// kendisi ilk erişimde veritabanından okunup istek boyunca saklanır.
public class CurrentUser
{
    public const string SessionKey = "Auth.UserId";

    private readonly IHttpContextAccessor _http;
    private readonly AppDbContext _db;
    private AppUser? _cached;
    private bool _loaded;

    public CurrentUser(IHttpContextAccessor http, AppDbContext db)
    {
        _http = http;
        _db = db;
    }

    public int? Id => _http.HttpContext?.Session.GetInt32(SessionKey);

    // Pasifleştirilmiş kullanıcı oturumu açık kalsa bile null döner
    public AppUser? User
    {
        get
        {
            if (_loaded) return _cached;
            _loaded = true;

            var id = Id;
            if (id != null)
                _cached = _db.Users.AsNoTracking().FirstOrDefault(u => u.Id == id && u.IsActive);

            return _cached;
        }
    }

    public bool IsAuthenticated => User != null;
    public UserRole? Role => User?.Role;
    public bool IsStaff => Role is UserRole.Agent or UserRole.Admin;
    public string DisplayName => User?.FullName ?? "";
}
