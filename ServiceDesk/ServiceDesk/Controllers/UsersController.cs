using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;

namespace ServiceDesk.Controllers;

// YÖNETİM: Kullanıcı listesi (sadece görüntüleme)
public class UsersController : Controller
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.Users.OrderBy(u => u.Role).ThenBy(u => u.FirstName).ToListAsync());
}
