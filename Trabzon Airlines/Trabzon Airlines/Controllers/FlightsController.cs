using System; // DateTime için
using System.Collections.Generic; // List koleksiyonları için
using System.Linq; // LINQ sorguları için
using System.Web; // HTTP context için
using System.Web.Mvc; // MVC controller için
using Trabzon_Airlines.Data; // Veritabanı bağlamı için
using Trabzon_Airlines.Models; // Model sınıfları için
using Newtonsoft.Json; // JSON serialization için

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
        // SEARCH - POST
        // Uçuş arama - POST metodu (JSON döndürür)
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        public JsonResult Search(string fromCode, string toCode, string departureDate, string returnDate = null, string tripType = "roundtrip")
        {
            try
            {
                // Tarih parse etme
                if (string.IsNullOrEmpty(departureDate))
                {
                    return Json(new { success = false, message = "Gidiş tarihi gereklidir." }, JsonRequestBehavior.AllowGet);
                }

                DateTime depDate;
                if (!DateTime.TryParse(departureDate, out depDate))
                {
                    return Json(new { success = false, message = "Geçersiz gidiş tarihi formatı." }, JsonRequestBehavior.AllowGet);
                }

                // Tarih aralığı belirle (günün başı ve sonu)
                // Set date range (start and end of day)
                DateTime depDateStart = depDate.Date; // Günün başı (00:00:00)
                DateTime depDateEnd = depDate.Date.AddDays(1).AddTicks(-1); // Günün sonu (23:59:59.9999999)

                // Gidiş uçuşlarını ara
                // Search for departure flights
                var departureFlights = db.Flights
                    .Where(f => f.IsActive &&
                                f.FromCode == fromCode &&
                                f.ToCode == toCode &&
                                f.DepartureTime >= depDateStart &&
                                f.DepartureTime <= depDateEnd)
                    .OrderBy(f => f.DepartureTime)
                    .ToList();

                List<Flight> returnFlights = null;

                // Gidiş-dönüş için dönüş uçuşlarını da ara
                // For round trip, also search for return flights
                if (tripType == "roundtrip" && !string.IsNullOrEmpty(returnDate))
                {
                    DateTime retDate;
                    if (DateTime.TryParse(returnDate, out retDate))
                    {
                        // Tarih aralığı belirle (günün başı ve sonu)
                        // Set date range (start and end of day)
                        DateTime retDateStart = retDate.Date; // Günün başı (00:00:00)
                        DateTime retDateEnd = retDate.Date.AddDays(1).AddTicks(-1); // Günün sonu (23:59:59.9999999)

                        returnFlights = db.Flights
                            .Where(f => f.IsActive &&
                                        f.FromCode == toCode &&
                                        f.ToCode == fromCode &&
                                        f.DepartureTime >= retDateStart &&
                                        f.DepartureTime <= retDateEnd)
                            .OrderBy(f => f.DepartureTime)
                            .ToList();
                    }
                }

                // Sonuçları JSON formatında döndür
                // Return results in JSON format
                var result = new
                {
                    success = true,
                    hasResults = departureFlights.Any(),
                    departureFlights = departureFlights.Select(f => new
                    {
                        flightId = f.FlightId,
                        flightNumber = f.FlightNumber,
                        fromCode = f.FromCode,
                        toCode = f.ToCode,
                        departureTime = f.DepartureTime.ToString("yyyy-MM-dd HH:mm"),
                        arrivalTime = f.ArrivalTime.ToString("yyyy-MM-dd HH:mm"),
                        price = f.Price,
                        weatherCondition = f.WeatherCondition,
                        delayRisk = f.DelayRisk
                    }).ToList(),
                    returnFlights = returnFlights != null ? returnFlights.Select(f => new
                    {
                        flightId = f.FlightId,
                        flightNumber = f.FlightNumber,
                        fromCode = f.FromCode,
                        toCode = f.ToCode,
                        departureTime = f.DepartureTime.ToString("yyyy-MM-dd HH:mm"),
                        arrivalTime = f.ArrivalTime.ToString("yyyy-MM-dd HH:mm"),
                        price = f.Price,
                        weatherCondition = f.WeatherCondition,
                        delayRisk = f.DelayRisk
                    }).ToList() : null
                };

                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Hata durumunda
                // In case of error
                return Json(new { success = false, message = "Arama sırasında bir hata oluştu: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // --------------------------
        // SEARCH RESULTS - GET
        // Uçuş arama sonuçları sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult SearchResults()
        {
            // Uçuş sonuçları sayfası view'ını döndür
            // Return flight search results view
            return View();
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
