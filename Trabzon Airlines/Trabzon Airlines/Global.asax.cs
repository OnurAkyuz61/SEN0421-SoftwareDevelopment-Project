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
using Trabzon_Airlines.Controllers; // Controller sınıfları için

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

        // Hata yakalama - 404 ve diğer hatalar için
        // Error handling - For 404 and other errors
        protected void Application_Error(object sender, EventArgs e)
        {
            // Son hatayı al
            // Get last error
            Exception exception = Server.GetLastError(); // Son hatayı al
            
            // Hata loglama - Debug için
            // Error logging - For debugging
            if (exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"Application Error: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {exception.StackTrace}");
                if (exception.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {exception.InnerException.Message}");
                }
            }
            
            HttpException httpException = exception as HttpException; // HTTP exception'a çevir

            // HTTP hatası kontrolü
            // Check if HTTP error
            if (httpException != null)
            {
                // HTTP status kodu
                // HTTP status code
                int httpCode = httpException.GetHttpCode(); // HTTP kodunu al

                // 404 hatası kontrolü
                // Check for 404 error
                if (httpCode == 404)
                {
                    // 404 sayfasına yönlendir
                    // Redirect to 404 page
                    Response.Clear(); // Response'u temizle
                    Server.ClearError(); // Hata durumunu temizle
                    Response.StatusCode = 404; // 404 status kodu ayarla
                    Response.TrySkipIisCustomErrors = true; // IIS custom errors'ı atla

                    // NotFound view'ını render et
                    // Render NotFound view
                    var routeData = new RouteData(); // Yeni route data oluştur
                    routeData.Values["controller"] = "Error"; // Controller adı
                    routeData.Values["action"] = "NotFound"; // Action adı

                    // Controller'ı oluştur ve view'ı render et
                    // Create controller and render view
                    IController errorController = new ErrorController(); // Error controller oluştur
                    var rc = new RequestContext(new HttpContextWrapper(Context), routeData); // Request context oluştur
                    errorController.Execute(rc); // Controller'ı çalıştır
                }
                else
                {
                    // Diğer HTTP hataları için genel error sayfası
                    // General error page for other HTTP errors
                    Response.Clear();
                    Server.ClearError();
                    Response.StatusCode = httpCode;
                    Response.TrySkipIisCustomErrors = true;
                    
                    var routeData = new RouteData();
                    routeData.Values["controller"] = "Error";
                    routeData.Values["action"] = "Index";
                    
                    IController errorController = new ErrorController();
                    var rc = new RequestContext(new HttpContextWrapper(Context), routeData);
                    errorController.Execute(rc);
                }
            }
            else if (exception != null)
            {
                // Genel exception için error sayfası
                // Error page for general exceptions
                Response.Clear();
                Server.ClearError();
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                
                var routeData = new RouteData();
                routeData.Values["controller"] = "Error";
                routeData.Values["action"] = "Index";
                
                IController errorController = new ErrorController();
                var rc = new RequestContext(new HttpContextWrapper(Context), routeData);
                errorController.Execute(rc);
            }
        }
    }
}

