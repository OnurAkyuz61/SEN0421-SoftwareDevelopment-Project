using System; // DateTime için
using System.Collections.Generic; // List koleksiyonları için
using System.Data.Entity; // Entity Framework için
using System.Linq; // LINQ sorguları için
using System.Net; // HTTP status kodları için
using System.Web; // HTTP context için
using System.Web.Mvc; // MVC controller için
using Trabzon_Airlines.Data; // Veritabanı bağlamı için
using Trabzon_Airlines.Models; // Model sınıfları için
using Trabzon_Airlines.Services; // Fiyat takibi için

namespace Trabzon_Airlines.Controllers
{
    // Admin Uçuş Yönetimi Controller'ı - Admin panelinde uçuş yönetimi
    // Admin Flights Management Controller - Flight management in admin panel
    public class AdminFlightsController : Controller
    {
        // Veritabanı bağlamı - TAContext sınıfından instance
        // Database context - Instance from TAContext class
        private TAContext db = new TAContext();

        // --------------------------
        // INDEX - GET
        // Uçuş listesi - GET metodu
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

            // Tüm uçuşları veritabanından getir (tarih sırasına göre)
            // Get all flights from database (ordered by date)
            var flights = db.Flights
                .OrderByDescending(f => f.DepartureTime) // Tarihe göre azalan sıralama
                .ToList();

            // Uçuş listesi view'ını döndür
            // Return flight list view
            return View(flights);
        }

        // --------------------------
        // CREATE - GET
        // Yeni uçuş ekleme sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Create()
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

            // Hava durumu ve rötar riski seçeneklerini ViewBag'e ekle
            // Add weather condition and delay risk options to ViewBag
            PopulateFlightStatusViewBags();

            // Uçuş ekleme view'ını döndür
            // Return flight creation view
            return View();
        }

        // --------------------------
        // CREATE - POST
        // Yeni uçuş ekleme işlemi - POST metodu
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        [ValidateAntiForgeryToken] // CSRF koruması için
        public ActionResult Create(Flight model)
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

            // Model validasyonu kontrolü
            // Check model validation
            if (!ModelState.IsValid)
            {
                // Form hatalıysa aynı hataları gösterir
                // If form is invalid, show same errors
                PopulateFlightStatusViewBags(); // ViewBag'i tekrar doldur
                return View(model);
            }

            // Uçuşu veritabanına ekle
            // Add flight to database
            db.Flights.Add(model);
            db.SaveChanges(); // FlightId burada oluşur

            // Koltukları FlightSeats tablosuna üret
            // Generate seats in FlightSeats table
            GenerateFlightSeats(model.FlightId);

            // Uçuş listesine yönlendir
            // Redirect to flight list
            return RedirectToAction("Index");
        }

        // --------------------------
        // DELETE - GET
        // Uçuş silme işlemi - GET metodu
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

            // Uçuş ID kontrolü
            // Check flight ID
            if (id == null)
            {
                // ID yoksa 400 Bad Request hatası döndür
                // If ID is null, return 400 Bad Request error
                return new HttpStatusCodeResult(400);
            }

            // Uçuşu veritabanından bul
            // Find flight from database
            var flight = db.Flights.Find(id);

            // Uçuş bulunamadıysa 404 hatası döndür
            // If flight not found, return 404 error
            if (flight == null)
                return HttpNotFound();

            // Uçuşu veritabanından sil
            // Delete flight from database
            db.Flights.Remove(flight);
            db.SaveChanges();

            // Uçuş listesine yönlendir
            // Redirect to flight list
            return RedirectToAction("Index");
        }

        // --------------------------
        // EDIT - GET
        // Uçuş düzenleme sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Edit(int? id)
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

            // Uçuş ID kontrolü
            // Check flight ID
            if (id == null)
            {
                // ID yoksa 400 Bad Request hatası döndür
                // If ID is null, return 400 Bad Request error
                return new HttpStatusCodeResult(400);
            }

            // Uçuşu veritabanından bul
            // Find flight from database
            var flight = db.Flights.Find(id);

            // Uçuş bulunamadıysa 404 hatası döndür
            // If flight not found, return 404 error
            if (flight == null)
                return HttpNotFound();

            // Hava durumu ve rötar riski seçeneklerini ViewBag'e ekle
            // Add weather condition and delay risk options to ViewBag
            PopulateFlightStatusViewBags();

            // Uçuş düzenleme view'ını döndür
            // Return flight edit view
            return View(flight);
        }

        // --------------------------
        // EDIT - POST
        // Uçuş düzenleme işlemi - POST metodu
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        [ValidateAntiForgeryToken] // CSRF koruması için
        public ActionResult Edit(Flight model)
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

            // Model validasyonu kontrolü
            // Check model validation
            if (!ModelState.IsValid)
            {
                // Form hatalıysa ViewBag'i tekrar doldur
                // If form is invalid, repopulate ViewBag
                PopulateFlightStatusViewBags();
                return View(model);
            }

            // Uçuş bilgilerini güncelle
            // Update flight information
            db.Entry(model).State = EntityState.Modified; // Entity state'i Modified olarak işaretle
            db.SaveChanges(); // Değişiklikleri kaydet

            // Fiyat düştüyse bu uçuşu takip eden kullanıcılara mesaj gönder
            // If price dropped, notify users watching this flight
            try
            {
                int notified = PriceWatchService.CheckFlight(db, model.FlightId);
                if (notified > 0)
                {
                    TempData["AdminSuccess_tr"] = notified + " kullanıcıya fiyat düştü bildirimi gönderildi.";
                    TempData["AdminSuccess_en"] = "Price drop alert sent to " + notified + " user(s).";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[PriceWatch] " + ex.Message);
            }

            // Uçuş listesine yönlendir
            // Redirect to flight list
            return RedirectToAction("Index");
        }

        // --------------------------
        // SEED SEATS - GET
        // Koltuk planını bir kere oluşturmak için - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult SeedSeats()
        {
            // Eğer koltuk tablosu boşsa koltukları oluştur
            // If seat table is empty, create seats
            if (!db.Seats.Any())
            {
                // 1–4 sıra Business sınıfı
                // 5–14 sıra Economy sınıfı
                // Her sırada A, B, C, D kolonları var

                // 14 sıra için döngü
                // Loop for 14 rows
                for (int row = 1; row <= 14; row++)
                {
                    // İlk 4 sıra Business, diğerleri Economy
                    // First 4 rows Business, others Economy
                    string cabin = row <= 4 ? "Business" : "Economy";

                    // Her sırada 4 kolon (A, B, C, D)
                    // 4 columns in each row (A, B, C, D)
                    foreach (var col in new[] { "A", "B", "C", "D" })
                    {
                        // Yeni koltuk oluştur
                        // Create new seat
                        db.Seats.Add(new Seat
                        {
                            SeatNumber = row + col, // "1A", "1B", ... şeklinde koltuk numarası
                            CabinClass = cabin // Kabin sınıfı
                        });
                    }
                }

                // Değişiklikleri kaydet
                // Save changes
                db.SaveChanges();
            }

            // Başarı mesajı döndür
            // Return success message
            return Content("Seat tablosu dolduruldu (eğer boşsa).");
        }

        // --------------------------
        // GENERATE FLIGHT SEATS - PRIVATE
        // Uçuş için koltukları oluştur - Özel metod
        // --------------------------
        private void GenerateFlightSeats(int flightId)
        {
            // Bu uçuş için FlightSeat kaydı zaten varsa tekrar oluşturma
            // If FlightSeat record already exists for this flight, don't create again
            if (db.FlightSeats.Any(fs => fs.FlightId == flightId))
                return; // Çıkış yap

            // Tüm koltukları veritabanından getir
            // Get all seats from database
            var seats = db.Seats.ToList();

            // Her koltuk için FlightSeat kaydı oluştur
            // Create FlightSeat record for each seat
            foreach (var seat in seats)
            {
                db.FlightSeats.Add(new FlightSeat
                {
                    FlightId = flightId, // Uçuş ID'si
                    SeatId = seat.SeatId, // Koltuk ID'si
                    IsReserved = false // Başlangıçta rezerve değil
                });
            }

            // Değişiklikleri kaydet
            // Save changes
            db.SaveChanges();
        }

        // --------------------------
        // SEATS - GET
        // Admin koltuk listesi - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Seats(int flightId)
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

            // Uçuşu veritabanından bul
            // Find flight from database
            var flight = db.Flights.Find(flightId);

            // Uçuş bulunamadıysa 404 hatası döndür
            // If flight not found, return 404 error
            if (flight == null)
                return HttpNotFound();

            // Seçilen uçuş için koltukları getir
            // Get seats for selected flight
            var seats = db.FlightSeats
                          .Where(x => x.FlightId == flightId) // Uçuş ID'sine göre filtrele
                          .OrderBy(x => x.Seat.SeatNumber) // Koltuk numarasına göre sırala
                          .ToList();

            // ViewBag'e uçuş bilgisini ekle
            // Add flight information to ViewBag
            ViewBag.Flight = flight;

            // Koltuk listesi view'ını döndür
            // Return seat list view
            return View(seats);
        }

        // --------------------------
        // SEAT DETAIL - GET
        // Koltuk detayı - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult SeatDetail(int id)
        {
            // Koltuk bilgisini veritabanından getir (uçuş ve koltuk bilgileri ile)
            // Get seat information from database (with flight and seat information)
            var fs = db.FlightSeats
                       .Include("Flight") // Uçuş bilgisini dahil et
                       .Include("Seat") // Koltuk bilgisini dahil et
                       .FirstOrDefault(x => x.FlightSeatId == id);

            // Koltuk bulunamadıysa 404 hatası döndür
            // If seat not found, return 404 error
            if (fs == null)
                return HttpNotFound();

            // Koltuk detay view'ını döndür
            // Return seat detail view
            return View(fs);
        }

        // --------------------------
        // UNRESERVE - GET
        // Koltuk rezervasyonunu kaldır - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Unreserve(int id)
        {
            // Koltuk bilgisini veritabanından bul
            // Find seat information from database
            var fs = db.FlightSeats.Find(id);

            // Koltuk bulunamadıysa 404 hatası döndür
            // If seat not found, return 404 error
            if (fs == null)
                return HttpNotFound();

            // Koltuk rezervasyonunu kaldır (boşalt)
            // Remove seat reservation (free it)
            fs.IsReserved = false;
            db.SaveChanges(); // Değişiklikleri kaydet

            // Koltuk detay sayfasına yönlendir
            // Redirect to seat detail page
            return RedirectToAction("SeatDetail", new { id });
        }

        // --------------------------
        // POPULATE FLIGHT STATUS VIEWBAGS - PRIVATE
        // Hava durumu seçeneklerini ViewBag'e ekle - Özel metod
        // --------------------------
        // --------------------------
        // REFRESH WEATHER - GET
        // Tüm yaklaşan uçuşların hava durumunu internetten çek ve riskleri güncelle
        // --------------------------
        [HttpGet]
        public ActionResult RefreshWeather()
        {
            if (Session["AdminId"] == null) return RedirectToAction("Login", "Admin");

            var r = WeatherScheduler.RunNow();
            if (r == null)
            {
                TempData["WeatherInfo_tr"] = "Güncelleme zaten çalışıyor veya hata oluştu. Output penceresinde [Weather] satırlarına bakın.";
                TempData["WeatherInfo_en"] = "Update already running or failed. Check [Weather] lines in the Output window.";
            }
            else
            {
                TempData["WeatherInfo_tr"] = string.Format("Hava durumu güncellendi: {0} uçuş, {1} yolcuya uyarı gönderildi{2}.",
                    r.Updated, r.Alerts, r.Failed > 0 ? ", " + r.Failed + " uçuşta hata" : "");
                TempData["WeatherInfo_en"] = string.Format("Weather updated: {0} flights, {1} passenger alerts sent{2}.",
                    r.Updated, r.Alerts, r.Failed > 0 ? ", " + r.Failed + " failed" : "");
            }
            return RedirectToAction("Index");
        }

        // --------------------------
        // SIMULATE WEATHER - GET (TEST)
        // Seçilen uçuş için fırtına simüle eder ve yolculara uyarı gönderir
        // --------------------------
        [HttpGet]
        public ActionResult SimulateWeather(int id)
        {
            if (Session["AdminId"] == null) return RedirectToAction("Login", "Admin");

            var flight = db.Flights.Find(id);
            if (flight == null) return HttpNotFound();

            int sent = WeatherService.Simulate(db, flight);
            TempData["WeatherInfo_tr"] = string.Format("{0} için TEST fırtınası simüle edildi. Rötar riski: Yüksek. {1} yolcuya uyarı mesajı gönderildi.", flight.FlightNumber, sent);
            TempData["WeatherInfo_en"] = string.Format("TEST storm simulated for {0}. Delay risk: High. {1} passenger alerts sent.", flight.FlightNumber, sent);
            return RedirectToAction("Index");
        }

        private void PopulateFlightStatusViewBags()
        {
            // Hava durumu seçenekleri - Dropdown list için
            // Weather condition options - For dropdown list
            ViewBag.WeatherOptions = new List<SelectListItem>
            {
                new SelectListItem { Text = "Seçiniz", Value = "" }, // Boş seçenek
                new SelectListItem { Text = "Güneşli", Value = "Güneşli" }, // Güneşli hava
                new SelectListItem { Text = "Yağmurlu", Value = "Yağmurlu" }, // Yağmurlu hava
                new SelectListItem { Text = "Karlı", Value = "Karlı" }, // Karlı hava
                // Otomatik hava durumu servisinin kullandığı ek etiketler
                new SelectListItem { Text = "Açık", Value = "Açık" },
                new SelectListItem { Text = "Parçalı Bulutlu", Value = "Parçalı Bulutlu" },
                new SelectListItem { Text = "Bulutlu", Value = "Bulutlu" },
                new SelectListItem { Text = "Sisli", Value = "Sisli" },
                new SelectListItem { Text = "Rüzgarlı", Value = "Rüzgarlı" },
                new SelectListItem { Text = "Fırtınalı", Value = "Fırtınalı" },
            };

            // Rötar riski seçenekleri - Dropdown list için
            // Delay risk options - For dropdown list
            ViewBag.DelayRiskOptions = new List<SelectListItem>
            {
                new SelectListItem { Text = "Seçiniz", Value = "" }, // Boş seçenek
                new SelectListItem { Text = "Düşük", Value = "Düşük" }, // Düşük risk
                new SelectListItem { Text = "Orta", Value = "Orta" }, // Orta risk
                new SelectListItem { Text = "Yüksek", Value = "Yüksek" } // Yüksek risk
            };
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
