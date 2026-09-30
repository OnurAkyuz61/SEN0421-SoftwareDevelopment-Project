using System; // Temel sistem sınıfları için
using System.Web; // HTTP context için
using System.Web.Mvc; // MVC framework için

namespace Trabzon_Airlines.Controllers
{
    // Hata Controller'ı - 404 ve diğer hatalar için
    // Error Controller - For 404 and other errors
    public class ErrorController : Controller
    {
        // 404 Sayfa Bulunamadı - NotFound view'ını döndürür
        // 404 Page Not Found - Returns NotFound view
        public ActionResult NotFound()
        {
            // Response status kodunu 404 olarak ayarla
            // Set response status code to 404
            Response.StatusCode = 404; // HTTP 404 status kodu
            Response.TrySkipIisCustomErrors = true; // IIS custom errors'ı atla

            // NotFound view'ını döndür
            // Return NotFound view
            return View("NotFound");
        }

        // Genel Hata Sayfası - Error view'ını döndürür
        // General Error Page - Returns Error view
        public ActionResult Index()
        {
            // Response status kodunu 500 olarak ayarla
            // Set response status code to 500
            Response.StatusCode = 500; // HTTP 500 status kodu
            Response.TrySkipIisCustomErrors = true; // IIS custom errors'ı atla

            // Error view'ını döndür
            // Return Error view
            return View("Error");
        }
    }
}

