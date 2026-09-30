using System; // DateTime için
using System.Linq; // LINQ sorguları için
using System.Web.Mvc; // MVC controller için
using Trabzon_Airlines.Data; // Veritabanı bağlamı için
using Trabzon_Airlines.Models; // Model sınıfları için
using System.Data.Entity; // Entity Framework için

namespace Trabzon_Airlines.Controllers
{
    // Admin Rezervasyon Yönetimi Controller'ı - Admin panelinde rezervasyon yönetimi
    // Admin Bookings Management Controller - Booking management in admin panel
    public class AdminBookingsController : Controller
    {
        // Veritabanı bağlamı - TAContext sınıfından instance
        // Database context - Instance from TAContext class
        private TAContext db = new TAContext();

        // --------------------------
        // INDEX - GET
        // Rezervasyon listesi - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Index()
        {
            // Session kontrolü - Giriş yapılmamışsa login sayfasına yönlendir
            // Session check - If not logged in, redirect to login page
            if (Session["AdminId"] == null)
            {
                TempData["AdminErrorKey"] = "NotLoggedIn";
                TempData["AdminError_tr"] = "Lütfen önce giriş yapın!";
                TempData["AdminError_en"] = "Please login first!";
                return RedirectToAction("Login", "Admin");
            }

            // Tüm rezervasyonları veritabanından getir (tarih sırasına göre)
            // Get all bookings from database (ordered by date)
            var bookings = db.Bookings
                .Include(b => b.Flight) // Uçuş bilgisini dahil et
                .Include(b => b.Seat) // Koltuk bilgisini dahil et
                .OrderByDescending(b => b.BookingDate) // Tarihe göre azalan sıralama
                .ToList();

            // Rezervasyon listesi view'ını döndür
            // Return booking list view
            return View(bookings);
        }

        // --------------------------
        // DETAILS - GET
        // Rezervasyon detayı - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Details(int? id)
        {
            // Session kontrolü - Giriş yapılmamışsa login sayfasına yönlendir
            // Session check - If not logged in, redirect to login page
            if (Session["AdminId"] == null)
            {
                TempData["AdminErrorKey"] = "NotLoggedIn";
                TempData["AdminError_tr"] = "Lütfen önce giriş yapın!";
                TempData["AdminError_en"] = "Please login first!";
                return RedirectToAction("Login", "Admin");
            }

            // Rezervasyon ID kontrolü
            // Check booking ID
            if (id == null)
            {
                // ID yoksa 400 Bad Request hatası döndür
                // If ID is null, return 400 Bad Request error
                return new HttpStatusCodeResult(400);
            }

            // Rezervasyonu veritabanından bul
            // Find booking from database
            var booking = db.Bookings
                .Include(b => b.Flight) // Uçuş bilgisini dahil et
                .Include(b => b.Seat) // Koltuk bilgisini dahil et
                .FirstOrDefault(b => b.BookingId == id);

            // Rezervasyon bulunamadıysa 404 hatası döndür
            // If booking not found, return 404 error
            if (booking == null)
                return HttpNotFound();

            // Rezervasyon detay view'ını döndür
            // Return booking detail view
            return View(booking);
        }

        // --------------------------
        // DELETE - GET
        // Rezervasyon silme işlemi - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Delete(int? id)
        {
            // Session kontrolü - Giriş yapılmamışsa login sayfasına yönlendir
            // Session check - If not logged in, redirect to login page
            if (Session["AdminId"] == null)
            {
                TempData["AdminErrorKey"] = "NotLoggedIn";
                TempData["AdminError_tr"] = "Lütfen önce giriş yapın!";
                TempData["AdminError_en"] = "Please login first!";
                return RedirectToAction("Login", "Admin");
            }

            // Rezervasyon ID kontrolü
            // Check booking ID
            if (id == null)
            {
                // ID yoksa 400 Bad Request hatası döndür
                // If ID is null, return 400 Bad Request error
                return new HttpStatusCodeResult(400);
            }

            // Rezervasyonu veritabanından bul
            // Find booking from database
            var booking = db.Bookings
                .Include(b => b.Flight) // Uçuş bilgisini dahil et
                .Include(b => b.Seat) // Koltuk bilgisini dahil et
                .FirstOrDefault(b => b.BookingId == id);

            // Rezervasyon bulunamadıysa 404 hatası döndür
            // If booking not found, return 404 error
            if (booking == null)
                return HttpNotFound();

            // İlgili FlightSeat kaydını bul ve rezervasyonu kaldır
            // Find related FlightSeat record and remove reservation
            var flightSeat = db.FlightSeats
                .FirstOrDefault(fs => fs.FlightId == booking.FlightId && fs.SeatId == booking.SeatId);

            // Eğer FlightSeat kaydı varsa rezervasyonu kaldır
            // If FlightSeat record exists, remove reservation
            if (flightSeat != null)
            {
                flightSeat.IsReserved = false; // Rezervasyonu kaldır
            }

            // Rezervasyonu veritabanından sil
            // Delete booking from database
            db.Bookings.Remove(booking);
            db.SaveChanges(); // Değişiklikleri kaydet

            // Başarı mesajı
            // Success message
            TempData["AdminSuccessKey"] = "BookingDeleted";
            TempData["AdminSuccess_tr"] = "Rezervasyon başarıyla silindi.";
            TempData["AdminSuccess_en"] = "Booking deleted successfully.";

            // Rezervasyon listesine yönlendir
            // Redirect to booking list
            return RedirectToAction("Index");
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

