using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Filters;

// Şifre max yaşını aşmış oturumları profil/şifre değiştirme dışındaki
// sayfalardan uzak tutar. Auth:PasswordMaxAgeDays ≤ 0 ise kapalıdır.
public class PasswordExpiryFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // AuthorizeRole reddettiyse tekrar yazma
        if (context.Result != null) return;

        var services = context.HttpContext.RequestServices;
        var current = services.GetRequiredService<CurrentUser>();
        var user = current.User;
        if (user is null) return;

        var cfg = services.GetRequiredService<IOptions<AuthSettings>>().Value;
        if (!PasswordPolicy.IsExpired(user, cfg)) return;

        if (IsExempt(context)) return;

        var endpoint = context.HttpContext.GetEndpoint();
        var isApi = endpoint?.Metadata.GetMetadata<ApiAuthorizeRoleAttribute>() != null;

        if (isApi)
        {
            context.Result = new JsonResult(new ApiError(
                "Şifrenizin süresi doldu. Profil sayfasından yeni şifre belirleyin."))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        var tempData = services.GetRequiredService<ITempDataDictionaryFactory>()
            .GetTempData(context.HttpContext);
        tempData["Err"] = PasswordPolicy.ExpiredMessage(cfg);

        context.Result = new RedirectToActionResult("Index", "Profile", null);
    }

    // Süresi dolmuşken yalnızca şifre yenileme ve çıkış serbest
    private static bool IsExempt(AuthorizationFilterContext context)
    {
        var rd = context.RouteData.Values;
        var controller = rd["controller"]?.ToString() ?? "";
        var action = rd["action"]?.ToString() ?? "";

        if (controller.Equals("Profile", StringComparison.OrdinalIgnoreCase) &&
            action.Equals("Index", StringComparison.OrdinalIgnoreCase) &&
            HttpMethods.IsGet(context.HttpContext.Request.Method))
            return true;

        if (controller.Equals("Profile", StringComparison.OrdinalIgnoreCase) &&
            action.Equals("ChangePassword", StringComparison.OrdinalIgnoreCase))
            return true;

        if (controller.Equals("Account", StringComparison.OrdinalIgnoreCase) &&
            (action.Equals("Logout", StringComparison.OrdinalIgnoreCase) ||
             action.Equals("KeepAlive", StringComparison.OrdinalIgnoreCase)))
            return true;

        // Bildirim rozeti poll'u sayfa yenilemeden kırılmasın
        if (controller.Equals("Notifications", StringComparison.OrdinalIgnoreCase) &&
            action.Equals("UnreadCount", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
