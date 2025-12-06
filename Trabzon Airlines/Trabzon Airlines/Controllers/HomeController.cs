using System; // DateTime için
using System.Collections.Generic; // Koleksiyonlar için
using System.Linq; // LINQ sorguları için
using System.Web; // HTTP context için
using System.Web.Mvc; // MVC framework için

namespace Trabzon_Airlines.Controllers
{
    // Ana sayfa controller'ı - Genel sayfa yönlendirmeleri
    // Home page controller - General page redirects
    public class HomeController : Controller
    {
        // --------------------------
        // INDEX - GET
        // Ana sayfa - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Index()
        {
            // Ana sayfa view'ını döndür
            // Return homepage view
            return View();
        }

        // --------------------------
        // ABOUT - GET
        // Hakkımızda sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult About()
        {
            // Hakkımızda view'ını döndür
            // Return about view
            return View();
        }

        // --------------------------
        // CONTACT - GET
        // İletişim sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Contact()
        {
            // İletişim view'ını döndür
            // Return contact view
            return View();
        }

        // --------------------------
        // LOGIN - GET
        // Giriş sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Login()
        {
            // Giriş view'ını döndür
            // Return login view
            return View();
        }

        // --------------------------
        // SIGNUP - GET
        // Kayıt sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Signup()
        {
            // Kayıt view'ını döndür
            // Return signup view
            return View();
        }

        // --------------------------
        // TRIP AI - GET
        // Seyahat AI sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult TripAI()
        {
            // Trip AI view'ını döndür
            // Return Trip AI view
            return View();  
        }

        // --------------------------
        // PRIVACY - GET
        // Gizlilik Politikası sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Privacy()
        {
            // Gizlilik politikası view'ını döndür
            // Return privacy policy view
            return View();
        }

        // --------------------------
        // SITEMAP - GET
        // Site haritası sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Sitemap()
        {
            // Site haritası view'ını döndür
            // Return sitemap view
            return View();
        }
    }

}
