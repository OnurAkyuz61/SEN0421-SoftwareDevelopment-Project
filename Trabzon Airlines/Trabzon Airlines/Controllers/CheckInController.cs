using System;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using Trabzon_Airlines.Data;
using Trabzon_Airlines.Models;

namespace Trabzon_Airlines.Controllers
{
    public class CheckInController : Controller
    {
        private TAContext db = new TAContext();

        // GET: CheckIn
        public ActionResult Index()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                // Hata durumunda ana sayfaya yönlendir
                System.Diagnostics.Debug.WriteLine($"CheckIn Index Error: {ex.Message}");
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: CheckIn/SearchBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SearchBooking(string pnrCode)
        {
            try
            {
                // PNR kodu validasyonu
                if (string.IsNullOrWhiteSpace(pnrCode))
                {
                    TempData["Error"] = "Lütfen geçerli bir PNR kodu girin.";
                    TempData["Error_en"] = "Please enter a valid PNR code.";
                    return RedirectToAction("Index");
                }

                // Veritabanı bağlantısını test et
                if (!TestDatabaseConnection())
                {
                    TempData["Error"] = "Veritabanı bağlantısı kurulamadı. Lütfen daha sonra tekrar deneyin.";
                    TempData["Error_en"] = "Database connection failed. Please try again later.";
                    return RedirectToAction("Index");
                }

                // PNR kodunu normalize et (büyük harfe çevir ve boşlukları temizle)
                pnrCode = pnrCode.ToUpper().Trim();

                // Booking'i veritabanından çek
                var booking = db.Bookings
                               .Include(b => b.Flight)
                               .Include(b => b.Seat)
                               .FirstOrDefault(b => b.PNR == pnrCode);

                if (booking == null)
                {
                    TempData["Error"] = "Rezervasyon bulunamadı. Lütfen PNR kodunuzu kontrol edin.";
                    TempData["Error_en"] = "Booking not found. Please check your PNR code.";
                    return RedirectToAction("Index");
                }

                // BookingMeals'ı yükle
                LoadBookingMeals(booking);

                // Uçuş tarihini kontrol et
                if (booking.Flight != null)
                {
                    var departureTime = booking.Flight.DepartureTime;
                    var now = DateTime.Now;
                    var checkInOpenTime = departureTime.AddHours(-24);

                    // Geçmiş tarihli uçuş kontrolü
                    if (departureTime < now)
                    {
                        TempData["Warning"] = "Uçuşunuz Check-In'e kapanmıştır. Geçmiş tarihli uçuşlar için check-in yapılamaz.";
                        TempData["Warning_en"] = "Your flight check-in has closed. Check-in cannot be performed for past flights.";
                    }
                    // Check-in henüz açılmamış kontrolü (24 saat kuralı)
                    else if (now < checkInOpenTime)
                    {
                        var hoursUntilCheckIn = (checkInOpenTime - now).TotalHours;
                        TempData["Warning"] = $"Check-in henüz açılmadı. Check-in uçuştan 24 saat önce açılır. (Kalan süre: {Math.Ceiling(hoursUntilCheckIn)} saat)";
                        TempData["Warning_en"] = $"Check-in is not yet open. Check-in opens 24 hours before departure. (Time remaining: {Math.Ceiling(hoursUntilCheckIn)} hours)";
                    }
                }

                return View("BookingDetails", booking);
            }
            catch (SqlException sqlEx)
            {
                System.Diagnostics.Debug.WriteLine($"CheckIn SearchBooking SQL Error: {sqlEx.Message}");
                TempData["Error"] = "Veritabanı hatası oluştu. Lütfen daha sonra tekrar deneyin.";
                TempData["Error_en"] = "Database error occurred. Please try again later.";
                return RedirectToAction("Index");
            }
            catch (EntityException entityEx)
            {
                System.Diagnostics.Debug.WriteLine($"CheckIn SearchBooking Entity Error: {entityEx.Message}");
                TempData["Error"] = "Veritabanı bağlantı hatası. Lütfen daha sonra tekrar deneyin.";
                TempData["Error_en"] = "Database connection error. Please try again later.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Hata detaylarını logla
                System.Diagnostics.Debug.WriteLine($"CheckIn SearchBooking Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                
                TempData["Error"] = "Bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                TempData["Error_en"] = "An error occurred. Please try again later.";
                return RedirectToAction("Index");
            }
        }

        // POST: CheckIn/DoCheckIn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DoCheckIn(int bookingId)
        {
            try
            {
                // Veritabanı bağlantısını test et
                if (!TestDatabaseConnection())
                {
                    TempData["Error"] = "Veritabanı bağlantısı kurulamadı. Lütfen daha sonra tekrar deneyin.";
                    TempData["Error_en"] = "Database connection failed. Please try again later.";
                    return RedirectToAction("Index");
                }

                // Booking'i veritabanından çek
                var booking = db.Bookings
                               .Include(b => b.Flight)
                               .Include(b => b.Seat)
                               .FirstOrDefault(b => b.BookingId == bookingId);

                if (booking == null)
                {
                    TempData["Error"] = "Rezervasyon bulunamadı.";
                    TempData["Error_en"] = "Booking not found.";
                    return RedirectToAction("Index");
                }

                // Zaten check-in yapılmış mı kontrolü
                if (booking.CheckedIn)
                {
                    TempData["Info"] = "Bu uçuş için zaten check-in yaptınız.";
                    TempData["Info_en"] = "You have already checked in for this flight.";
                    return RedirectToAction("BoardingPass", new { bookingId = bookingId });
                }

                // Check-in validasyonları
                if (booking.Flight != null)
                {
                    var departureTime = booking.Flight.DepartureTime;
                    var now = DateTime.Now;
                    var checkInOpenTime = departureTime.AddHours(-24);

                    // Geçmiş tarihli uçuş kontrolü
                    if (departureTime < now)
                    {
                        TempData["Error"] = "Geçmiş tarihli uçuşlar için check-in yapılamaz.";
                        TempData["Error_en"] = "Check-in cannot be performed for past flights.";
                        LoadBookingMeals(booking);
                        return View("BookingDetails", booking);
                    }

                    // Check-in henüz açılmamış kontrolü (24 saat kuralı)
                    if (now < checkInOpenTime)
                    {
                        var hoursUntilCheckIn = (checkInOpenTime - now).TotalHours;
                        TempData["Error"] = $"Check-in henüz açılmadı. Check-in uçuştan 24 saat önce açılır. (Kalan süre: {Math.Ceiling(hoursUntilCheckIn)} saat)";
                        TempData["Error_en"] = $"Check-in is not yet open. Check-in opens 24 hours before departure. (Time remaining: {Math.Ceiling(hoursUntilCheckIn)} hours)";
                        LoadBookingMeals(booking);
                        return View("BookingDetails", booking);
                    }
                }

                // BookingMeals'ı yükle
                LoadBookingMeals(booking);

                // Check-in işlemini gerçekleştir
                booking.CheckedIn = true;
                booking.CheckInTime = DateTime.Now;
                db.SaveChanges();

                TempData["Success"] = "Check-in başarılı!";
                TempData["Success_en"] = "Check-in successful!";
                return RedirectToAction("BoardingPass", new { bookingId = bookingId });
            }
            catch (SqlException sqlEx)
            {
                System.Diagnostics.Debug.WriteLine($"CheckIn DoCheckIn SQL Error: {sqlEx.Message}");
                TempData["Error"] = "Veritabanı hatası oluştu. Lütfen daha sonra tekrar deneyin.";
                TempData["Error_en"] = "Database error occurred. Please try again later.";
                return RedirectToAction("Index");
            }
            catch (EntityException entityEx)
            {
                System.Diagnostics.Debug.WriteLine($"CheckIn DoCheckIn Entity Error: {entityEx.Message}");
                TempData["Error"] = "Veritabanı bağlantı hatası. Lütfen daha sonra tekrar deneyin.";
                TempData["Error_en"] = "Database connection error. Please try again later.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckIn DoCheckIn Error: {ex.Message}");
                TempData["Error"] = "Check-in sırasında bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                TempData["Error_en"] = "An error occurred during check-in. Please try again later.";
                return RedirectToAction("Index");
            }
        }

        // GET: CheckIn/BoardingPass
        public ActionResult BoardingPass(int bookingId)
        {
            try
            {
                // Veritabanı bağlantısını test et
                if (!TestDatabaseConnection())
                {
                    TempData["Error"] = "Veritabanı bağlantısı kurulamadı. Lütfen daha sonra tekrar deneyin.";
                    TempData["Error_en"] = "Database connection failed. Please try again later.";
                    return RedirectToAction("Index");
                }

                var booking = db.Bookings
                               .Include(b => b.Flight)
                               .Include(b => b.Seat)
                               .FirstOrDefault(b => b.BookingId == bookingId);

                if (booking == null)
                {
                    TempData["Error"] = "Rezervasyon bulunamadı.";
                    TempData["Error_en"] = "Booking not found.";
                    return RedirectToAction("Index");
                }

                if (!booking.CheckedIn)
                {
                    TempData["Error"] = "Lütfen önce check-in yapın.";
                    TempData["Error_en"] = "Please complete check-in first.";
                    return RedirectToAction("Index");
                }

                // BookingMeals'ı yükle
                LoadBookingMeals(booking);

                return View(booking);
            }
            catch (SqlException sqlEx)
            {
                System.Diagnostics.Debug.WriteLine($"CheckIn BoardingPass SQL Error: {sqlEx.Message}");
                TempData["Error"] = "Veritabanı hatası oluştu. Lütfen daha sonra tekrar deneyin.";
                TempData["Error_en"] = "Database error occurred. Please try again later.";
                return RedirectToAction("Index");
            }
            catch (EntityException entityEx)
            {
                System.Diagnostics.Debug.WriteLine($"CheckIn BoardingPass Entity Error: {entityEx.Message}");
                TempData["Error"] = "Veritabanı bağlantı hatası. Lütfen daha sonra tekrar deneyin.";
                TempData["Error_en"] = "Database connection error. Please try again later.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Hata detaylarını logla
                System.Diagnostics.Debug.WriteLine($"CheckIn BoardingPass Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                
                TempData["Error"] = "Bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                TempData["Error_en"] = "An error occurred. Please try again later.";
                return RedirectToAction("Index");
            }
        }

        // BookingMeals'ı yükle (helper method)
        private void LoadBookingMeals(Booking booking)
        {
            try
            {
                db.Entry(booking).Collection(b => b.BookingMeals).Load();
                if (booking.BookingMeals != null && booking.BookingMeals.Count > 0)
                {
                    foreach (var bookingMeal in booking.BookingMeals.ToList())
                    {
                        if (bookingMeal != null)
                        {
                            try
                            {
                                db.Entry(bookingMeal).Reference(bm => bm.Meal).Load();
                            }
                            catch (Exception mealEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Meal Load Error: {mealEx.Message}");
                                // Meal yüklenemezse devam et
                            }
                        }
                    }
                }
            }
            catch (Exception mealsEx)
            {
                System.Diagnostics.Debug.WriteLine($"BookingMeals Load Error: {mealsEx.Message}");
                // BookingMeals yüklenemezse devam et, view'da null check var
            }
        }

        // Veritabanı bağlantısını test et
        private bool TestDatabaseConnection()
        {
            try
            {
                var connection = db.Database.Connection;
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }
                return connection.State == System.Data.ConnectionState.Open;
            }
            catch (SqlException sqlEx)
            {
                System.Diagnostics.Debug.WriteLine($"SQL Connection Error: {sqlEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database Connection Error: {ex.Message}");
                return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (db != null)
                {
                    db.Dispose();
                    db = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}

