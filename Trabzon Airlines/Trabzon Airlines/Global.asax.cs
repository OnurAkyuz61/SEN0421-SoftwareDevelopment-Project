using System; // Temel sistem sınıfları için
using System.Collections.Generic; // Koleksiyonlar için
using System.Data.Entity; // Entity Framework için
using System.Linq; // LINQ sorguları için
using System.Text; // Encoding için
using System.Web; // HTTP context için
using System.Web.Mvc; // MVC framework için
using System.Web.Optimization; // Bundle için
using System.Web.Routing; // Routing için
using Trabzon_Airlines.Data; // Veritabanı bağlamı için
using Trabzon_Airlines.Migrations; // Migration'lar için

namespace Trabzon_Airlines
{
    // MVC Uygulama sınıfı - Global uygulama ayarları
    // MVC Application class - Global application settings
    public class MvcApplication : System.Web.HttpApplication
    {
        // Uygulama başlangıcı - Uygulama ilk yüklendiğinde çalışır
        // Application start - Runs when application is first loaded
        protected void Application_Start()
        {
            // Veritabanı migration'larını otomatik uygula
            // Automatically apply database migrations
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<TAContext, Configuration>());

            // Tüm area'ları kaydet
            // Register all areas
            AreaRegistration.RegisterAllAreas();

            // Global filtreleri kaydet
            // Register global filters
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);

            // Route'ları kaydet
            // Register routes
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            // Bundle'ları kaydet
            // Register bundles
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        // Her istek başlangıcında çalışır - Encoding ayarları
        // Runs at the beginning of each request - Encoding settings
        protected void Application_BeginRequest()
        {
            // Türkçe karakter desteği için UTF-8 encoding ayarla
            // Set UTF-8 encoding for Turkish character support
            Response.ContentEncoding = Encoding.UTF8;
            Response.Charset = "utf-8";
        }
    }
}

