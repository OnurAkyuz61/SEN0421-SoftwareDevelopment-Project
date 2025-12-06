using System; // DateTime için
using System.Collections.Generic; // List koleksiyonları için
using System.Linq; // LINQ sorguları için
using System.Web; // HTTP context için
using System.Web.Mvc; // MVC controller için
using Trabzon_Airlines.Data; // Veritabanı bağlamı için

namespace Trabzon_Airlines.Controllers
{
    // Uçuş controller'ı - Müşterilere uçuş listesi ve durum bilgisi
    // Flights controller - Flight list and status information for customers
    public class FlightsController : Controller
    {
        // Veritabanı bağlamı - TAContext sınıfından instance
        // Database context - Instance from TAContext class
        private TAContext db = new TAContext();

        // --------------------------
        // INDEX - GET
        // Aktif uçuşları listele - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Index()
        {
            // Aktif uçuşları veritabanından getir
            // Get active flights from database
            var flights = db.Flights.Where(x => x.IsActive).ToList();

            // Uçuş listesi view'ını döndür
            // Return flight list view
            return View(flights);
        }

        // --------------------------
        // STATUS - GET
        // Tüm uçuşları durum bilgisi ile listele - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Status()
        {
            // Tüm uçuşları veritabanından getir (aktif ve pasif)
            // Get all flights from database (active and inactive)
            var flights = db.Flights.ToList();

            // Uçuş durum view'ını döndür
            // Return flight status view
            return View(flights);
        }

        // --------------------------
        // DISPOSE - KAYNAKLARI TEMİZLE
        // Dispose - Clean up resources
        // --------------------------
        protected override void Dispose(bool disposing)
        {
            // Eğer disposing true ise veritabanı bağlantısını kapat
            // If disposing is true, close database connection
            if (disposing)
            {
                db.Dispose(); // Veritabanı bağlantısını kapat
            }

            // Base dispose metodunu çağır
            // Call base dispose method
            base.Dispose(disposing);
        }
    }
}
