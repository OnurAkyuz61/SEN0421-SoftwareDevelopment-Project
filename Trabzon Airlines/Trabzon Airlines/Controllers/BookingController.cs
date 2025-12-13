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

            // Yolcu sayıları ve kabin sınıfını Session'dan veya localStorage'dan al (JavaScript ile)
            // Get passenger counts and cabin class from Session or localStorage (via JavaScript)
            // Bu bilgiler JavaScript tarafında işlenecek
            // This information will be processed on the JavaScript side

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
            // Kullanıcı giriş kontrolü
            if (Session["UserId"] == null)
            {
                TempData["Error_tr"] = "Satın alma işlemi için lütfen giriş yapın veya kayıt olun.";
                TempData["Error_en"] = "Please login or register to complete the purchase.";

                string returnUrl = Url.Action("SelectSeat", "Booking", new { flightId = FlightId });
                return RedirectToAction("Login", "Home", new { returnUrl = returnUrl });
            }

            // Koltuk seçilmiş mi?
            if (SelectedSeatId == null)
            {
                TempData["Error"] = "Lütfen bir koltuk seçin.";
                return RedirectToAction("SelectSeat", new { flightId = FlightId });
            }

            //  Seçilen koltuğu Seat ve Flight ile birlikte getir
            var fs = db.FlightSeats
                       .Include("Seat")
                       .Include("Flight")
                       .FirstOrDefault(x => x.FlightId == FlightId && x.SeatId == SelectedSeatId);

            if (fs == null)
                return HttpNotFound();

            // Koltuk daha önce alınmış mı
            if (fs.IsReserved)
            {
                TempData["Error"] = "Bu koltuk az önce başka biri tarafından alındı.";
                return RedirectToAction("SelectSeat", new { flightId = FlightId });
            }

            // Koltuğu geçici olarak rezerve et
            fs.IsReserved = true;
            db.SaveChanges();

            //  Koltuk sınıfına göre BİRİM FİYAT belirle
            decimal basePrice = fs.Seat.CabinClass == "Business"
                ? fs.Flight.BusinessPrice
                : fs.Flight.EconomyPrice;

            // Yolcu sayıları varsayılan
            int adultCount = 1;
            int childCount = 0;
            int infantCount = 0;
            string cabinClass = fs.Seat.CabinClass;

            // Formdan yolcu sayılarını al
            if (!string.IsNullOrEmpty(Request.Form["AdultCount"]))
                int.TryParse(Request.Form["AdultCount"], out adultCount);

            if (!string.IsNullOrEmpty(Request.Form["ChildCount"]))
                int.TryParse(Request.Form["ChildCount"], out childCount);

            if (!string.IsNullOrEmpty(Request.Form["InfantCount"]))
                int.TryParse(Request.Form["InfantCount"], out infantCount);

            // Infant ücretsiz = sadece adult + child
            int totalPassengers = adultCount + childCount;

            //  TOPLAM FİYAT
            decimal totalPrice = basePrice * totalPassengers;

            // Rezervasyon bilgisini Session kaydet
            Session["PendingBooking"] = new
            {
                FlightId = fs.FlightId,
                SeatId = fs.SeatId,
                FlightNumber = fs.Flight.FlightNumber,
                FromCode = fs.Flight.FromCode,
                ToCode = fs.Flight.ToCode,
                DepartureTime = fs.Flight.DepartureTime,
                ArrivalTime = fs.Flight.ArrivalTime,
                SeatNumber = fs.Seat.SeatNumber,
                CabinClass = cabinClass,
                AdultCount = adultCount,
                ChildCount = childCount,
                InfantCount = infantCount,
                BasePrice = basePrice,
                TotalPrice = totalPrice
            };

            // Ödeme sayfasına yönlendir
            return RedirectToAction("Index", "Payment", new { amount = totalPrice });
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
