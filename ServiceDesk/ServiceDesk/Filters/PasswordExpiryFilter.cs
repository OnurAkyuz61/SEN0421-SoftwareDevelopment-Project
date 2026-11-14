using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Filters;

// İlk giriş veya süresi dolmuş şifrede kullanıcıyı profil/şifre
// değiştirme dışındaki sayfalardan uzak tutar.
public class PasswordExpiryFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.Result != null) return;

        var services = context.HttpContext.RequestServices;
        var current = services.GetRequiredService<CurrentUser>();
        var user = current.User;
        if (user is null) return;

        var cfg = services.GetRequiredService<IOptions<AuthSettings>>().Value;
        if (!PasswordPolicy.RequiresChange(user, cfg)) return;

        if (IsExempt(context)) return;

        var message = PasswordPolicy.RequiredMessage(user, cfg);
        var endpoint = context.HttpContext.GetEndpoint();
        var isApi = endpoint?.Metadata.GetMetadata<ApiAuthorizeRoleAttribute>() != null;

        if (isApi)
        {
            context.Result = new JsonResult(new ApiError(message))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        var tempData = services.GetRequiredService<ITempDataDictionaryFactory>()
            .GetTempData(context.HttpContext);
        tempData["Err"] = message;

        context.Result = new RedirectToActionResult("Index", "Profile", null);
    }

    // Zorunlu yenilemede yalnızca şifre değiştirme ve çıkış serbest
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

        if (controller.Equals("Notifications", StringComparison.OrdinalIgnoreCase) &&
            action.Equals("UnreadCount", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
