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

    public async Task<IActionResult> Index() =>
        View(await _db.Users.OrderBy(u => u.Role).ThenBy(u => u.FirstName).ToListAsync());
}
