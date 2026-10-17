using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;

namespace ServiceDesk.Controllers;

// YÖNETİM: Kullanıcı listesi (sadece görüntüleme)
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class UsersController : Controller
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        // Son giriş denemeleri: güvenlik denetim izi
        ViewBag.LoginAttempts = await _db.LoginAttempts.AsNoTracking()
            .OrderByDescending(l => l.AttemptedAt).Take(20).ToListAsync();

        return View(await _db.Users
            .AsNoTracking()
            .Include(u => u.Department)
            .Include(u => u.Team)
            .OrderBy(u => u.Role).ThenBy(u => u.FirstName)
            .ToListAsync());
    }
}
