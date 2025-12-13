using System; // DateTime için
using System.Collections.Generic; // Koleksiyonlar için
using System.Linq; // LINQ sorguları için
using System.Web; // HTTP context için
using System.Web.Mvc; // MVC framework için
using Trabzon_Airlines.Data;   // TAContext için
using Trabzon_Airlines.Models; // SupportRequest için


namespace Trabzon_Airlines.Controllers
{
    // Ana sayfa controller'ı - Genel sayfa yönlendirmeleri
    // Home page controller - General page redirects
    public class HomeController : Controller
    {
        // Destek taleplerini listelemek için DB context
        private TAContext db = new TAContext();

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

        //Destek sayfası - GET
        [HttpGet]
        public ActionResult Support()
        {
            ViewBag.Title = "Destek";
            ViewBag.TitleEn = "Support";

            // Varsayılan
            ViewBag.IsLoggedIn = false;
            ViewBag.MySupportTickets = null;

            // Kullanıcı login ise kendi taleplerini çek
            if (Session["UserId"] != null)
            {
                int userId;
                if (int.TryParse(Session["UserId"].ToString(), out userId))
                {
                    var myTickets = db.SupportRequests
                                      .Where(x => x.UserId == userId)
                                      .OrderByDescending(x => x.CreatedAt)
                                      .Take(10)
                                      .ToList();

                    ViewBag.MySupportTickets = myTickets;
                }

                ViewBag.IsLoggedIn = true;
            }

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
        // KVKK - GET
        // KVKK Aydınlatma Metni sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult KVKK()
        {
            // KVKK aydınlatma metni view'ını döndür
            // Return KVKK clarification text view
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
