using Microsoft.AspNetCore.Mvc;

namespace ServiceDesk.Controllers;

public class AccountController : Controller
{
    public IActionResult Login() => View();
}
