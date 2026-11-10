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
//
// JSON uçları 302 yönlendirme beklemediği için aşağıda 401/403 döndüren
// varyantı var: ApiAuthorizeRoleAttribute. Yetki kuralının kendisi (oturum
// var mı, rol yetiyor mu) yalnızca burada yazılı; alt sınıf sadece yanıtı
// değiştirir ki iki süzgeç zamanla birbirinden ayrı düşmesin.
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
            context.Result = Unauthenticated(context, current);
            return;
        }

        if (_allowed.Length > 0 && !_allowed.Contains(user.Role))
            context.Result = Forbidden(context);
    }

    // Oturum yok ya da süresi dolmuş
    protected virtual IActionResult Unauthenticated(AuthorizationFilterContext context, CurrentUser current)
    {
        // GET isteklerinde kullanıcı girişten sonra bulunduğu sayfaya geri döner
        var req = context.HttpContext.Request;
        var returnUrl = HttpMethods.IsGet(req.Method) ? req.PathBase + req.Path + req.QueryString : null;

        return new RedirectToActionResult("Login", "Account", new
        {
            returnUrl,
            reason = current.ExpiredThisRequest ? "expired" : null
        });
    }

    // Oturum geçerli ama rol yetmiyor
    protected virtual IActionResult Forbidden(AuthorizationFilterContext context)
    {
        var tempData = context.HttpContext.RequestServices
            .GetRequiredService<ITempDataDictionaryFactory>()
            .GetTempData(context.HttpContext);
        tempData["Err"] = "Bu sayfaya erişim yetkiniz yok.";

        return new RedirectToActionResult("Index", "Portal", null);
    }
}

// JSON uçlarının yetki süzgeci. Rol kuralı AuthorizeRole ile birebir aynıdır,
// değişen yalnızca yanıt: HTML sayfası beklemeyen istemciyi giriş ekranına
// 302 ile atmak yerine durum kodu + ApiError gövdesi döner.
//
//   401  oturum yok veya süresi dolmuş  → { "error": "…" }
//   403  oturum var, rol yetmiyor       → { "error": "…" }
//
//   [ApiAuthorizeRole]                 → her oturum açmış kullanıcı
//   [ApiAuthorizeRole(UserRole.Admin)] → yalnızca yönetici
//
// 401 / 403 ayrımı istemci için anlamlı: 401'de yeniden giriş gerekir,
// 403'te oturum geçerlidir ama yetki yoktur — isteği tekrarlamak bir şey
// değiştirmez. 302 dönseydi istemci giriş sayfasının HTML'ini "veri" sanıp
// JSON ayrıştırma hatası alırdı.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApiAuthorizeRoleAttribute : AuthorizeRoleAttribute
{
    public ApiAuthorizeRoleAttribute(params UserRole[] allowed) : base(allowed) { }

    protected override IActionResult Unauthenticated(AuthorizationFilterContext context, CurrentUser current) =>
        Error(StatusCodes.Status401Unauthorized, current.ExpiredThisRequest
            ? "Oturumunuzun süresi doldu. Yeniden giriş yapın."
            : "Bu uç için giriş yapmanız gerekiyor.");

    protected override IActionResult Forbidden(AuthorizationFilterContext context) =>
        Error(StatusCodes.Status403Forbidden, "Bu işlem için yetkiniz yok.");

    // Gövde api/* uçlarının geri kalanıyla aynı şekilde: { "error": "…" }
    private static JsonResult Error(int status, string message) =>
        new(new ApiError(message)) { StatusCode = status };
}
