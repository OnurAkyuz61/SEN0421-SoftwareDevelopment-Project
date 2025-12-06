using System.Linq; // LINQ sorguları için
using System.Web.Mvc; // MVC controller için
using Trabzon_Airlines.Data; // Veritabanı bağlamı için
using Trabzon_Airlines.Models; // Model sınıfları için
using System.Security.Cryptography; // Şifre hash'leme için
using System.Text; // String işlemleri için

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

            // Admin sayfası olduğunu belirt
            // Indicate that this is an admin page
            ViewBag.IsAdminPage = true;

            // Dashboard view'ını döndür
            // Return Dashboard view
            return View("Dashboard");
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
