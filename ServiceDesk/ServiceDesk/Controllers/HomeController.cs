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

    // Tüm hata yanıtlarının tek çıkışı:
    //   • yakalanan istisna      → UseExceptionHandler("/Home/Error")      (code boş, 500)
    //   • gövdesiz durum kodları → UseStatusCodePagesWithReExecute (?code=404 gibi)
    // Yanıtın durum kodu korunur: tarayıcı ve arama motoru 404'ü 200 sanmaz.
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code)
    {
        var status = code ?? Response.StatusCode;
        if (status < 400) status = StatusCodes.Status500InternalServerError;

        Response.StatusCode = status;

        return View(ErrorViewModel.For(status, Activity.Current?.Id ?? HttpContext.TraceIdentifier));
    }
}
