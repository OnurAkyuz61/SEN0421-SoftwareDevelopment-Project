using System.Linq; // LINQ sorguları için
using System.Web.Mvc; // MVC controller için
using Trabzon_Airlines.Data; // Veritabanı bağlamı için
using Trabzon_Airlines.Models; // Model sınıfları için
using System.Security.Cryptography; // Şifre hash'leme için
using System.Text; // String işlemleri için

namespace Trabzon_Airlines.Controllers
{
    // Hesap yönetimi controller'ı - Kullanıcı kayıt ve giriş işlemleri
    // Account management controller - User registration and login operations
    public class AccountController : Controller
    {
        // Veritabanı bağlamı - TAContext sınıfından instance
        // Database context - Instance from TAContext class
        private TAContext db = new TAContext();

        // --------------------------
        // SIGNUP (REGISTER) - POST
        // Kullanıcı kayıt işlemi - POST metodu
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        [ValidateAntiForgeryToken] // CSRF koruması için
        public ActionResult Signup(User model)
        {
            // Model validasyonu kontrolü
            // Check model validation
            if (!ModelState.IsValid)
            {
                // Model geçersizse kayıt sayfasına yönlendir
                // If model is invalid, redirect to signup page
                return RedirectToAction("Signup", "Home");
            }

            // E-posta zaten kayıtlı mı kontrol et
            // Check if email is already registered
            var existing = db.Users.FirstOrDefault(x => x.Email == model.Email);
            if (existing != null)
            {
                // E-posta zaten kayıtlıysa hata mesajı göster
                // If email is already registered, show error message
                TempData["Error_tr"] = "Bu e-posta adresi zaten kayıtlı!";
                TempData["Error_en"] = "This email address is already registered!";
                return RedirectToAction("Signup", "Home");
            }

            // Şifreyi hash'le - SHA256 algoritması ile
            // Hash the password - Using SHA256 algorithm
            model.Password = HashPassword(model.Password);

            // Kullanıcıyı veritabanına kaydet
            // Save user to database
            db.Users.Add(model);
            db.SaveChanges();

            // Başarı mesajı göster
            // Show success message
            TempData["Success_tr"] = "Kayıt başarılı! Giriş yapabilirsiniz.";
            TempData["Success_en"] = "Registration successful! You can now log in.";

            // Giriş sayfasına yönlendir
            // Redirect to login page
            return RedirectToAction("Login", "Home");
        }

        // --------------------------
        // LOGIN - POST
        // Kullanıcı giriş işlemi - POST metodu
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        [ValidateAntiForgeryToken] // CSRF koruması için
        [ValidateInput(false)] // HTML input validasyonunu kapat
        public ActionResult Login(string email, string password)
        {
            // Şifreyi hash'le - SHA256 algoritması ile
            // Hash the password - Using SHA256 algorithm
            string hashedPassword = HashPassword(password);

            // Veritabanından kullanıcıyı bul
            // Find user from database
            var user = db.Users.FirstOrDefault(x => x.Email == email && x.Password == hashedPassword);

            // Kullanıcı bulunamadıysa hata mesajı göster
            // If user not found, show error message
            if (user == null)
            {
                TempData["Error_tr"] = "E-posta veya şifre hatalı!";
                TempData["Error_en"] = "Incorrect email or password!";
                return RedirectToAction("Login", "Home");
            }

            // Giriş başarılı - Session'a kullanıcı bilgilerini kaydet
            // Login successful - Save user information to session
            Session["UserId"] = user.UserId;
            Session["UserName"] = user.FirstName;
            Session["UserEmail"] = user.Email;

            // Ana sayfaya yönlendir
            // Redirect to homepage
            return RedirectToAction("Index", "Home");
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
