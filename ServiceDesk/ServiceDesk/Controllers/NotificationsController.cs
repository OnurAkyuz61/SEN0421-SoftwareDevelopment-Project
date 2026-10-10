using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Data;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

public class NotificationsController : Controller
{
    private readonly AppDbContext _db;
    public NotificationsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() => View(await NotificationService.GetAsync(_db));
}
