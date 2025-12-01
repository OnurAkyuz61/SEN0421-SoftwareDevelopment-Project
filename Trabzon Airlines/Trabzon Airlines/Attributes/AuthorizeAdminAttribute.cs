using System;
using System.Web;
using System.Web.Mvc;

namespace Trabzon_Airlines.Attributes
{
    // Admin yetkilendirme attribute'u - Sadece giriş yapmış adminler erişebilir
    // Admin authorization attribute - Only logged in admins can access
    public class AuthorizeAdminAttribute : AuthorizeAttribute
    {
        // Yetkilendirme kontrolü
        // Authorization check
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            // Session'da AdminId var mı kontrol et
            // Check if AdminId exists in session
            return httpContext.Session["AdminId"] != null;
        }

        // Yetkilendirme başarısız olduğunda
        // When authorization fails
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            // Admin giriş sayfasına yönlendir
            // Redirect to admin login page
            filterContext.Result = new RedirectResult("~/Admin/Login");
        }
    }
}

