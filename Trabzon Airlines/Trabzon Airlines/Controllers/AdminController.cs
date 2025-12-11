using System; // DateTime için
using System.Linq; // LINQ sorguları için
using System.Web.Mvc; // MVC controller için
using Trabzon_Airlines.Data; // Veritabanı bağlamı için
using Trabzon_Airlines.Models; // Model sınıfları için
using System.Security.Cryptography; // Şifre hash'leme için
using System.Text; // String işlemleri için
using System.Data.Entity; // Entity Framework için (DbFunctions için)

namespace Trabzon_Airlines.Controllers
{
    // Admin Controller - Yönetici paneli için controller sınıfı
    // Admin Controller - Controller class for administrator panel
    public class AdminController : Controller
    {
        // Veritabanı bağlamı - TAContext sınıfından instance
        // Database context - Instance from TAContext class
        TAContext db = new TAContext();

        // --------------------------
        // ADMIN LOGIN - GET
        // Admin giriş sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Login()
        {
            // Eğer zaten giriş yapılmışsa dashboard'a yönlendir
            // If already logged in, redirect to dashboard
            if (Session["AdminId"] != null)
            {
                return RedirectToAction("Dashboard");
            }

            // AdminLogin view'ını döndür
            // Return AdminLogin view
            return View("AdminLogin");
        }

        // --------------------------
        // ADMIN LOGIN - POST
        // Admin giriş işlemi - POST metodu
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        [ValidateAntiForgeryToken] // CSRF koruması için
        public ActionResult Login(string email, string password)
        {
            // E-posta ve şifre boş kontrolü
            // Check if email and password are empty
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["AdminErrorKey"] = "EmptyFields";
                TempData["AdminError_tr"] = "E-posta ve şifre gereklidir!";
                TempData["AdminError_en"] = "Email and password are required!";
                return RedirectToAction("Login");
            }

            // Şifreyi hash'le - SHA256 algoritması ile
            // Hash the password - Using SHA256 algorithm
            string hashedPassword = HashPassword(password);

            // Veritabanından admin kullanıcısını bul
            // Find admin user from database
            var admin = db.Admins.FirstOrDefault(x => x.Email == email && x.Password == hashedPassword);

            // Admin bulunamadıysa hata mesajı göster
            // If admin not found, show error message
            if (admin == null)
            {
                TempData["AdminErrorKey"] = "WrongCredentials";
                TempData["AdminError_tr"] = "E-posta veya şifre hatalı!";
                TempData["AdminError_en"] = "Email or password is incorrect!";
                return RedirectToAction("Login");
            }

            // Giriş başarılı - Session'a admin bilgilerini kaydet
            // Login successful - Save admin information to session
            Session["AdminId"] = admin.AdminId;
            Session["AdminName"] = admin.FullName;
            Session["AdminEmail"] = admin.Email;

            // Başarı mesajı
            // Success message
            TempData["AdminSuccessKey"] = "LoginSuccess";
            TempData["AdminSuccess_tr"] = "Giriş başarılı! Admin paneline yönlendirildiniz.";
            TempData["AdminSuccess_en"] = "Login successful! You have been redirected to the admin panel.";

            // Dashboard'a yönlendir
            // Redirect to dashboard
            return RedirectToAction("Dashboard");
        }

        // --------------------------
        // ADMIN DASHBOARD
        // Admin ana sayfa - Dashboard
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Dashboard()
        {
            // Session kontrolü - Giriş yapılmamışsa login sayfasına yönlendir
            // Session check - If not logged in, redirect to login page
            if (Session["AdminId"] == null)
            {
                TempData["AdminErrorKey"] = "NotLoggedIn";
                TempData["AdminError_tr"] = "Lütfen önce giriş yapın!";
                TempData["AdminError_en"] = "Please login first!";
                return RedirectToAction("Login");
            }

            // İstatistikleri veritabanından çek
            // Get statistics from database
            var totalFlights = db.Flights.Count(); // Toplam uçuş sayısı
            var totalBookings = db.Bookings.Count(); // Toplam rezervasyon sayısı
            var totalUsers = db.Users.Count(); // Toplam kullanıcı sayısı
            var totalAdmins = db.Admins.Count(); // Toplam admin sayısı
            
            // Bugünkü rezervasyonlar
            // Today's bookings
            var todayBookings = db.Bookings.Count(b => DbFunctions.TruncateTime(b.BookingDate) == DbFunctions.TruncateTime(DateTime.Now));
            
            // Bu ayki rezervasyonlar
            // This month's bookings
            var thisMonthBookings = db.Bookings.Count(b => b.BookingDate.Year == DateTime.Now.Year && b.BookingDate.Month == DateTime.Now.Month);
            
            // Son 5 rezervasyon
            // Last 5 bookings
            var recentBookings = db.Bookings
                .OrderByDescending(b => b.BookingDate)
                .Take(5)
                .Select(b => new RecentBookingViewModel
                {
                    BookingId = b.BookingId,
                    PNR = b.PNR,
                    PassengerName = b.PassengerName,
                    PassengerSurname = b.PassengerSurname,
                    BookingDate = b.BookingDate,
                    FlightNumber = b.Flight.FlightNumber,
                    FromCode = b.Flight.FromCode,
                    ToCode = b.Flight.ToCode
                })
                .ToList();
            
            // Son 5 uçuş
            // Last 5 flights
            var recentFlights = db.Flights
                .OrderByDescending(f => f.DepartureTime)
                .Take(5)
                .Select(f => new RecentFlightViewModel
                {
                    FlightId = f.FlightId,
                    FlightNumber = f.FlightNumber,
                    FromCode = f.FromCode,
                    ToCode = f.ToCode,
                    DepartureTime = f.DepartureTime,
                    ArrivalTime = f.ArrivalTime,
                    Price = f.Price
                })
                .ToList();

            // ViewBag'e istatistikleri ekle
            // Add statistics to ViewBag
            ViewBag.TotalFlights = totalFlights; // Toplam uçuş
            ViewBag.TotalBookings = totalBookings; // Toplam rezervasyon
            ViewBag.TotalUsers = totalUsers; // Toplam kullanıcı
            ViewBag.TotalAdmins = totalAdmins; // Toplam admin
            ViewBag.TodayBookings = todayBookings; // Bugünkü rezervasyonlar
            ViewBag.ThisMonthBookings = thisMonthBookings; // Bu ayki rezervasyonlar
            ViewBag.RecentBookings = recentBookings; // Son rezervasyonlar
            ViewBag.RecentFlights = recentFlights; // Son uçuşlar

            // Admin sayfası olduğunu belirt
            // Indicate that this is an admin page
            ViewBag.IsAdminPage = true;

            // Dashboard view'ını döndür
            // Return Dashboard view
            return View("Dashboard");
        }

        // --------------------------
        // ADMIN SETTINGS - GET
        // Admin ayarlar sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Settings()
        {
            // Session kontrolü - Giriş yapılmamışsa login sayfasına yönlendir
            // Session check - If not logged in, redirect to login page
            if (Session["AdminId"] == null)
            {
                TempData["AdminErrorKey"] = "NotLoggedIn";
                TempData["AdminError_tr"] = "Lütfen önce giriş yapın!";
                TempData["AdminError_en"] = "Please login first!";
                return RedirectToAction("Login");
            }

            // Admin bilgilerini veritabanından getir
            // Get admin information from database
            int adminId = (int)Session["AdminId"]; // Session'dan admin ID'sini al
            var admin = db.Admins.Find(adminId); // Admin'i veritabanından bul

            // Admin bulunamadıysa hata mesajı göster
            // If admin not found, show error message
            if (admin == null)
            {
                TempData["AdminErrorKey"] = "AdminNotFound";
                TempData["AdminError_tr"] = "Admin bulunamadı!";
                TempData["AdminError_en"] = "Admin not found!";
                return RedirectToAction("Login");
            }

            // Ayarlar view'ını döndür
            // Return settings view
            return View(admin);
        }

        // --------------------------
        // ADMIN SETTINGS - POST
        // Admin ayarlar güncelleme - POST metodu
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        [ValidateAntiForgeryToken] // CSRF koruması için
        public ActionResult Settings(string fullName, string email, string currentPassword, string newPassword, string confirmPassword)
        {
            // Session kontrolü - Giriş yapılmamışsa login sayfasına yönlendir
            // Session check - If not logged in, redirect to login page
            if (Session["AdminId"] == null)
            {
                TempData["AdminErrorKey"] = "NotLoggedIn";
                TempData["AdminError_tr"] = "Lütfen önce giriş yapın!";
                TempData["AdminError_en"] = "Please login first!";
                return RedirectToAction("Login");
            }

            // Admin bilgilerini veritabanından getir
            // Get admin information from database
            int adminId = (int)Session["AdminId"]; // Session'dan admin ID'sini al
            var admin = db.Admins.Find(adminId); // Admin'i veritabanından bul

            // Admin bulunamadıysa hata mesajı göster
            // If admin not found, show error message
            if (admin == null)
            {
                TempData["AdminErrorKey"] = "AdminNotFound";
                TempData["AdminError_tr"] = "Admin bulunamadı!";
                TempData["AdminError_en"] = "Admin not found!";
                return RedirectToAction("Login");
            }

            // Şifre değiştirme kontrolü
            // Password change check
            if (!string.IsNullOrEmpty(newPassword))
            {
                // Mevcut şifre kontrolü
                // Current password check
                string hashedCurrentPassword = HashPassword(currentPassword); // Mevcut şifreyi hash'le
                if (admin.Password != hashedCurrentPassword)
                {
                    TempData["AdminErrorKey"] = "WrongPassword";
                    TempData["AdminError_tr"] = "Mevcut şifre hatalı!";
                    TempData["AdminError_en"] = "Current password is incorrect!";
                    return View(admin);
                }

                // Yeni şifre ve onay şifresi eşleşme kontrolü
                // New password and confirm password match check
                if (newPassword != confirmPassword)
                {
                    TempData["AdminErrorKey"] = "PasswordMismatch";
                    TempData["AdminError_tr"] = "Yeni şifre ve onay şifresi eşleşmiyor!";
                    TempData["AdminError_en"] = "New password and confirm password do not match!";
                    return View(admin);
                }

                // Yeni şifreyi hash'le ve kaydet
                // Hash and save new password
                admin.Password = HashPassword(newPassword); // Yeni şifreyi hash'le
            }

            // Admin bilgilerini güncelle
            // Update admin information
            admin.FullName = fullName; // Tam adı güncelle
            admin.Email = email; // E-postayı güncelle
            db.SaveChanges(); // Değişiklikleri kaydet

            // Session'ı güncelle
            // Update session
            Session["AdminName"] = admin.FullName; // Admin adını güncelle
            Session["AdminEmail"] = admin.Email; // Admin e-postasını güncelle

            // Başarı mesajı
            // Success message
            TempData["AdminSuccessKey"] = "SettingsUpdated";
            TempData["AdminSuccess_tr"] = "Ayarlar başarıyla güncellendi.";
            TempData["AdminSuccess_en"] = "Settings updated successfully.";

            // Ayarlar sayfasına yönlendir
            // Redirect to settings page
            return RedirectToAction("Settings");
        }

        // --------------------------
        // ADMIN LOGOUT
        // Admin çıkış işlemi
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Logout()
        {
            // Session'ı temizle
            // Clear session
            Session.Clear();
            Session.Abandon();

            // Çıkış başarı mesajı
            // Logout success message
            TempData["AdminSuccessKey"] = "LoggedOut";
            TempData["AdminSuccess_tr"] = "Oturum başarıyla kapatıldı.";
            TempData["AdminSuccess_en"] = "You have been logged out successfully.";

            // Login sayfasına yönlendir
            // Redirect to login page
            return RedirectToAction("Login");
        }

        // --------------------------
        // ŞİFRE HASH'LEME FONKSİYONU
        // Password hashing function - SHA256
        // --------------------------
        private string HashPassword(string password)
        {
            // SHA256 hash algoritması kullanarak şifreyi hash'le
            // Hash the password using SHA256 hash algorithm
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // Şifreyi byte array'e çevir
                // Convert password to byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));

                // Byte array'i hexadecimal string'e çevir
                // Convert byte array to hexadecimal string
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2")); // Her byte'ı 2 haneli hex'e çevir
                }

                // Hash'lenmiş şifreyi döndür
                // Return hashed password
                return builder.ToString();
            }
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
