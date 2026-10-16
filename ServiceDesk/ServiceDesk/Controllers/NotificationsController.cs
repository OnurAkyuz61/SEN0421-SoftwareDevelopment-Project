using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class NotificationsController : Controller
{
    private readonly AppDbContext _db;
    public NotificationsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() => View(await NotificationService.GetAsync(_db));
}
