using System; // DateTime için
using System.Collections.Generic; // List koleksiyonları için
using System.Data.Entity; // Entity Framework Include için
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
        // Koltuk seçim işlemi - POST metodu (çoklu koltuk seçimi destekler)
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        [ValidateAntiForgeryToken] // CSRF koruması için
        public ActionResult SelectSeat(int? FlightId, int[] SelectedSeatIds)
        {
            // Debug: Form verilerini logla
            // Debug: Log form data
            System.Diagnostics.Debug.WriteLine($"SelectSeat POST - FlightId: {FlightId}");
            System.Diagnostics.Debug.WriteLine($"SelectSeat POST - SelectedSeatIds: {(SelectedSeatIds != null ? string.Join(", ", SelectedSeatIds) : "null")}");
            
            // Kullanıcı giriş kontrolü
            if (Session["UserId"] == null)
            {
                TempData["Error_tr"] = "Satın alma işlemi için lütfen giriş yapın veya kayıt olun.";
                TempData["Error_en"] = "Please login or register to complete the purchase.";

                string returnUrl = Url.Action("SelectSeat", "Booking", new { flightId = FlightId });
                return RedirectToAction("Login", "Home", new { returnUrl = returnUrl });
            }

            // Koltuk seçilmiş mi? (Form'dan da kontrol et)
            // Is seat selected? (Also check from form)
            if (SelectedSeatIds == null || SelectedSeatIds.Length == 0)
            {
                // Form'dan da dene
                // Try from form too
                var seatIdsFromForm = Request.Form["SelectedSeatIds"];
                if (seatIdsFromForm != null && !string.IsNullOrEmpty(seatIdsFromForm))
                {
                    SelectedSeatIds = seatIdsFromForm.Split(',').Select(s => int.Parse(s.Trim())).ToArray();
                }
                
                if (SelectedSeatIds == null || SelectedSeatIds.Length == 0)
                {
                    TempData["Error_tr"] = "Lütfen en az bir koltuk seçin.";
                    TempData["Error_en"] = "Please select at least one seat.";
                    return RedirectToAction("SelectSeat", new { flightId = FlightId });
                }
            }

            // Formdan yolcu sayılarını al
            int adultCount = 1;
            int childCount = 0;
            int infantCount = 0;
            
            if (!string.IsNullOrEmpty(Request.Form["AdultCount"]))
                int.TryParse(Request.Form["AdultCount"], out adultCount);

            if (!string.IsNullOrEmpty(Request.Form["ChildCount"]))
                int.TryParse(Request.Form["ChildCount"], out childCount);

            if (!string.IsNullOrEmpty(Request.Form["InfantCount"]))
                int.TryParse(Request.Form["InfantCount"], out infantCount);

            // Infant ücretsiz = sadece adult + child
            int totalPassengers = adultCount + childCount;

            // Seçilen koltuk sayısı yolcu sayısıyla eşleşmeli
            if (SelectedSeatIds.Length != totalPassengers)
            {
                TempData["Error_tr"] = $"Seçtiğiniz koltuk sayısı ({SelectedSeatIds.Length}) yolcu sayısı ({totalPassengers}) ile eşleşmiyor. Lütfen {totalPassengers} koltuk seçin.";
                TempData["Error_en"] = $"Selected seat count ({SelectedSeatIds.Length}) does not match passenger count ({totalPassengers}). Please select {totalPassengers} seats.";
                return RedirectToAction("SelectSeat", new { flightId = FlightId });
            }

            // Uçuş bilgisini al
            var flight = db.Flights.Find(FlightId);
            if (flight == null)
                return HttpNotFound();

            // Seçilen tüm koltukları kontrol et ve getir
            var selectedSeats = new List<FlightSeat>();
            var selectedSeatInfo = new List<object>(); // Session için anonim tip listesi
            decimal totalPrice = 0;
            string cabinClass = null;

            foreach (var seatId in SelectedSeatIds)
            {
                var fs = db.FlightSeats
                           .Include(x => x.Seat)
                           .Include(x => x.Flight)
                           .FirstOrDefault(x => x.FlightId == FlightId && x.SeatId == seatId);

                if (fs == null)
                {
                    TempData["Error_tr"] = "Seçilen koltuklardan biri bulunamadı.";
                    TempData["Error_en"] = "One of the selected seats was not found.";
                    return RedirectToAction("SelectSeat", new { flightId = FlightId });
                }

                // Koltuk daha önce alınmış mı
                if (fs.IsReserved)
                {
                    TempData["Error_tr"] = $"Koltuk {fs.Seat.SeatNumber} az önce başka biri tarafından alındı.";
                    TempData["Error_en"] = $"Seat {fs.Seat.SeatNumber} was just taken by someone else.";
                    return RedirectToAction("SelectSeat", new { flightId = FlightId });
                }

                // Koltuk sınıfına göre BİRİM FİYAT belirle
                decimal basePrice = fs.Seat.CabinClass == "Business"
                    ? fs.Flight.BusinessPrice
                    : fs.Flight.EconomyPrice;

                // İlk koltuktan kabin sınıfını al (tüm koltuklar aynı sınıfta olmalı)
                if (cabinClass == null)
                    cabinClass = fs.Seat.CabinClass;

                // Her koltuk için fiyat ekle (1 yolcu = 1 koltuk)
                totalPrice += basePrice;

                // FlightSeat listesine ekle
                selectedSeats.Add(fs);
                
                // Session için anonim tip oluştur
                selectedSeatInfo.Add(new
                {
                    SeatId = fs.SeatId,
                    SeatNumber = fs.Seat.SeatNumber,
                    BasePrice = basePrice
                });
            }

            // Koltuğu geçici olarak rezerve et
            foreach (var fs in selectedSeats)
            {
                fs.IsReserved = true;
            }
            db.SaveChanges();

            // Rezervasyon bilgisini Session kaydet (çoklu koltuk için)
            Session["PendingBooking"] = new
            {
                FlightId = flight.FlightId,
                SelectedSeatIds = SelectedSeatIds, // Tüm seçili koltuk ID'leri
                SelectedSeats = selectedSeatInfo, // Koltuk detayları (anonim tip listesi)
                FlightNumber = flight.FlightNumber,
                FromCode = flight.FromCode,
                ToCode = flight.ToCode,
                DepartureTime = flight.DepartureTime,
                ArrivalTime = flight.ArrivalTime,
                CabinClass = cabinClass,
                AdultCount = adultCount,
                ChildCount = childCount,
                InfantCount = infantCount,
                TotalPrice = totalPrice
            };

            // Ödeme sayfasına yönlendir
            return RedirectToAction("Index", "Payment", new { amount = totalPrice });
        }

        // --------------------------
        // MEAL SELECTION - GET
        // Yemek seçim sayfası - GET metodu (Tamamen statik - veritabanı sorgusu yok)
        // --------------------------
        [HttpGet]
        public ActionResult MealSelection(int? bookingId)
        {
            // TAMAMEN STATİK SAYFA - Veritabanı sorgusu yapılmıyor
            // COMPLETELY STATIC PAGE - No database query
            
            // BookingId'yi ViewBag'e gönder (view'da kullanılabilir ama zorunlu değil)
            // Send bookingId to ViewBag (can be used in view but not required)
            ViewBag.BookingId = bookingId;
            ViewBag.Booking = null; // Her zaman null - statik sayfa
            
            // Boş model gönder (view'da statik yemek listesi var)
            // Send empty model (view has static meal list)
            return View(new List<Meal>());
        }

        // --------------------------
        // MEAL SELECTION - POST
        // Yemek seçim işlemi - POST metodu
        // --------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MealSelection(int? bookingId, int[] selectedMeals)
        {
            // Statik yemek seçimi - Veritabanına kaydetmeden devam et
            // Static meal selection - Continue without saving to database
            // Seçilen yemekler sadece istatistik için kullanılabilir
            
            // Form'dan bookingId'yi de kontrol et (boş string gelirse null yap)
            // Also check bookingId from form (set to null if empty string)
            if (!bookingId.HasValue)
            {
                string bookingIdStr = Request.Form["bookingId"];
                if (!string.IsNullOrEmpty(bookingIdStr) && int.TryParse(bookingIdStr, out int parsedBookingId))
                {
                    bookingId = parsedBookingId;
                }
            }
            
            // bookingId varsa BookingResult sayfasına yönlendir
            // If bookingId exists, redirect to BookingResult page
            if (bookingId.HasValue && bookingId.Value > 0)
            {
                try
                {
                    // Booking var mı kontrol et
                    // Check if booking exists
                    var booking = db.Bookings.FirstOrDefault(b => b.BookingId == bookingId.Value);
                    if (booking != null)
                    {
                        return RedirectToAction("BookingResult", new { bookingId = bookingId.Value });
                    }
                }
                catch
                {
                    // Hata durumunda ana sayfaya yönlendir
                    // In case of error, redirect to home
                }
            }
            
            // bookingId yoksa veya booking bulunamadıysa ana sayfaya yönlendir
            // If bookingId doesn't exist or booking not found, redirect to home
            TempData["Success_tr"] = "Yemek seçiminiz alındı. Teşekkür ederiz!";
            TempData["Success_en"] = "Your meal selection has been received. Thank you!";
            return RedirectToAction("Index", "Home");
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
                            .Include(b => b.Flight) // Uçuş bilgisini dahil et
                            .Include(b => b.Seat) // Koltuk bilgisini dahil et
                            .Include(b => b.BookingMeals.Select(bm => bm.Meal)) // Yemek bilgilerini dahil et
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
