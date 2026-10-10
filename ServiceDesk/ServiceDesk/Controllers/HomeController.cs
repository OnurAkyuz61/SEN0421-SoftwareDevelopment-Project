using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Models;

namespace ServiceDesk.Controllers;

public class HomeController : Controller
{
    // Ön karşılama sayfası (uygulama ilk açıldığında "/")
    public IActionResult Index() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
