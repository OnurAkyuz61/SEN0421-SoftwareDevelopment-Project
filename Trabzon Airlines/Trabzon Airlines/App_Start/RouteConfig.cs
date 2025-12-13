using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Trabzon_Airlines
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            // Statik dosyaları ignore et
            // Ignore static files
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // 404 hatası için özel route - En üste ekle
            // Special route for 404 error - Add at the top
            routes.MapRoute(
                name: "NotFound",
                url: "Error/NotFound",
                defaults: new { controller = "Error", action = "NotFound" }
            );

            // Varsayılan route - Tüm controller/action/id kombinasyonları için
            // Default route - For all controller/action/id combinations
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
