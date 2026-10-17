using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Filters;

// Oturum yoksa giriş sayfasına yollar; rol yetmiyorsa kullanıcıyı kendi
// alanına geri gönderir. Parametresiz kullanım "giriş yapmış herkes" demektir.
//   [AuthorizeRole]                                 → her oturum açmış kullanıcı
//   [AuthorizeRole(UserRole.Agent, UserRole.Admin)] → yalnızca destek ve yönetici
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly UserRole[] _allowed;

    public AuthorizeRoleAttribute(params UserRole[] allowed) => _allowed = allowed;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var services = context.HttpContext.RequestServices;
        var current = services.GetRequiredService<CurrentUser>();
        var user = current.User;

        if (user == null)
        {
            // GET isteklerinde kullanıcı girişten sonra bulunduğu sayfaya geri döner
            var req = context.HttpContext.Request;
            var returnUrl = HttpMethods.IsGet(req.Method) ? req.PathBase + req.Path + req.QueryString : null;

            context.Result = new RedirectToActionResult("Login", "Account", new
            {
                returnUrl,
                reason = current.ExpiredThisRequest ? "expired" : null
            });
            return;
        }

        if (_allowed.Length > 0 && !_allowed.Contains(user.Role))
        {
            var tempData = services.GetRequiredService<ITempDataDictionaryFactory>()
                                   .GetTempData(context.HttpContext);
            tempData["Err"] = "Bu sayfaya erişim yetkiniz yok.";

            context.Result = new RedirectToActionResult("Index", "Portal", null);
        }
    }
}
