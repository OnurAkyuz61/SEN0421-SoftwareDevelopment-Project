using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

public class HomeController : Controller
{
    private readonly CurrentUser _current;

    public HomeController(CurrentUser current) => _current = current;

    // Ön karşılama sayfası (uygulama ilk açıldığında "/").
    // Oturum açıksa kullanıcı doğrudan kendi alanına gider.
    public IActionResult Index()
    {
        if (_current.User is { } user)
            return user.Role == UserRole.User
                ? RedirectToAction("Index", "Portal")
                : RedirectToAction("Index", "Dashboard");

        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
