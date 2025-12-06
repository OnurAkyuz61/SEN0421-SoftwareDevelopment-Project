using System; // DateTime için
using System.Collections.Generic; // List koleksiyonları için
using System.Linq; // LINQ sorguları için
using System.Net; // HTTP status kodları için
using System.Web; // HTTP context için
using System.Web.Mvc; // MVC controller için
using Trabzon_Airlines.Data; // Veritabanı bağlamı için
using Trabzon_Airlines.Models; // Model sınıfları için

namespace Trabzon_Airlines.Controllers
{
    // Rezervasyon controller'ı - Uçuş rezervasyon işlemleri
    // Booking controller - Flight reservation operations
    public class BookingController : Controller
    {
        // Veritabanı bağlamı - TAContext sınıfından instance
        // Database context - Instance from TAContext class
        private TAContext db = new TAContext();

        // --------------------------
        // SELECT SEAT - GET
        // Koltuk seçim sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult SelectSeat(int? flightId)
        {
            // Uçuş ID kontrolü
            // Check flight ID
            if (flightId == null)
            {
                // Uçuş ID yoksa 400 Bad Request hatası döndür
                // If flight ID is null, return 400 Bad Request error
                return new HttpStatusCodeResult(400);
            }

            // Seçilen uçuş için koltukları getir
            // Get seats for selected flight
            var seats = db.FlightSeats
                          .Where(x => x.FlightId == flightId) // Uçuş ID'sine göre filtrele
                          .OrderBy(x => x.Seat.SeatNumber) // Koltuk numarasına göre sırala
                          .ToList();

            // ViewBag'e uçuş bilgisini ekle
            // Add flight information to ViewBag
            ViewBag.Flight = db.Flights.Find(flightId);

            // Koltuk seçim view'ını döndür
            // Return seat selection view
            return View(seats);
        }

        // --------------------------
        // SELECT SEAT - POST
        // Koltuk seçim işlemi - POST metodu
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        [ValidateAntiForgeryToken] // CSRF koruması için
        public ActionResult SelectSeat(int? FlightId, int? SelectedSeatId)
        {
            // Seçilen koltuk kontrolü
            // Check selected seat
            if (SelectedSeatId == null)
            {
                // Koltuk seçilmemişse hata mesajı göster
                // If seat is not selected, show error message
                TempData["Error"] = "Lütfen bir koltuk seçin.";
                return RedirectToAction("SelectSeat", new { flightId = FlightId });
            }

            // Seçilen koltuk ve uçuş bilgisini veritabanından getir
            // Get selected seat and flight information from database
            var fs = db.FlightSeats
                       .FirstOrDefault(x => x.FlightId == FlightId && x.SeatId == SelectedSeatId);

            // Koltuk bulunamadıysa 404 hatası döndür
            // If seat not found, return 404 error
            if (fs == null) return HttpNotFound();

            // Koltuk zaten rezerve edilmiş mi kontrol et
            // Check if seat is already reserved
            if (fs.IsReserved)
            {
                // Koltuk rezerve edilmişse hata mesajı göster
                // If seat is reserved, show error message
                TempData["Error"] = "Bu koltuk az önce başka biri tarafından alındı.";
                return RedirectToAction("SelectSeat", new { flightId = FlightId });
            }

            // Koltuğu rezerve et (kilitliyoruz)
            // Reserve the seat (lock it)
            fs.IsReserved = true;

            // Benzersiz PNR kodu üret
            // Generate unique PNR code
            string pnr = GeneratePNR();

            // Yeni rezervasyon oluştur
            // Create new booking
            var booking = new Booking
            {
                FlightId = FlightId.Value, // Uçuş ID'si
                SeatId = SelectedSeatId.Value, // Koltuk ID'si
                PassengerName = "Test", // Yolcu adı (şimdilik test)
                PassengerSurname = "User", // Yolcu soyadı (şimdilik test)
                PNR = pnr, // PNR kodu
                BookingDate = DateTime.Now // Rezervasyon tarihi
            };

            // Rezervasyonu veritabanına ekle
            // Add booking to database
            db.Bookings.Add(booking);
            db.SaveChanges();

            // Rezervasyon sonuç sayfasına yönlendir
            // Redirect to booking result page
            return RedirectToAction("BookingResult", new { bookingId = booking.BookingId });
        }

        // --------------------------
        // BOOKING RESULT - GET
        // Rezervasyon sonucu sayfası (PNR gösterme) - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult BookingResult(int bookingId)
        {
            // Rezervasyon bilgisini veritabanından getir (uçuş ve koltuk bilgileri ile)
            // Get booking information from database (with flight and seat information)
            var booking = db.Bookings
                            .Include("Flight") // Uçuş bilgisini dahil et
                            .Include("Seat") // Koltuk bilgisini dahil et
                            .FirstOrDefault(b => b.BookingId == bookingId);

            // Rezervasyon bulunamadıysa 404 hatası döndür
            // If booking not found, return 404 error
            if (booking == null)
                return HttpNotFound();

            // Rezervasyon sonuç view'ını döndür
            // Return booking result view
            return View(booking);
        }

        // --------------------------
        // PNR ÜRETİCİ YARDIMCI FONKSİYON
        // PNR generator helper function
        // --------------------------
        private string GeneratePNR()
        {
            // PNR için kullanılacak karakterler
            // Characters to use for PNR
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random(); // Rastgele sayı üretici

            string pnr; // PNR değişkeni
            do
            {
                // 6 karakterlik rastgele PNR kodu üret
                // Generate 6-character random PNR code
                pnr = new string(
                    Enumerable.Repeat(chars, 6) // 6 karakter
                              .Select(s => s[random.Next(s.Length)]) // Her karakter için rastgele seç
                              .ToArray()
                );
            }
            // Aynı PNR kodu veritabanında varsa tekrar üret
            // If same PNR code exists in database, generate again
            while (db.Bookings.Any(b => b.PNR == pnr));

            // Üretilen PNR kodunu döndür
            // Return generated PNR code
            return pnr;
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
