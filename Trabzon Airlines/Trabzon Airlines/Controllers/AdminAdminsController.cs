using System; // DateTime için
using System.Collections.Generic; // List koleksiyonları için
using System.Data.Entity; // Entity Framework için
using System.Linq; // LINQ sorguları için
using System.Net; // HTTP status kodları için
using System.Web; // HTTP context için
using System.Web.Mvc; // MVC controller için
using Trabzon_Airlines.Data; // Veritabanı bağlamı için
using Trabzon_Airlines.Models; // Model sınıfları için
using System.Security.Cryptography; // Şifre hash'leme için
using System.Text; // String işlemleri için

namespace Trabzon_Airlines.Controllers
{
    // Admin Admin Yönetimi Controller'ı - Admin panelinde admin yönetimi
    // Admin Admins Management Controller - Admin management in admin panel
    public class AdminAdminsController : Controller
    {
        // Veritabanı bağlamı - TAContext sınıfından instance
        // Database context - Instance from TAContext class
        private TAContext db = new TAContext();

        // --------------------------
        // INDEX - GET
        // Admin listesi - GET metodu
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

            // Tüm adminleri veritabanından getir (ID'ye göre azalan sıralama)
            // Get all admins from database (ordered by ID descending)
            var admins = db.Admins
                .OrderByDescending(a => a.AdminId) // ID'ye göre azalan sıralama
                .ToList();

            // Admin listesi view'ını döndür
            // Return admin list view
            return View(admins);
        }

        // --------------------------
        // CREATE - GET
        // Yeni admin ekleme sayfası - GET metodu
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

            // Yeni admin modeli oluştur
            // Create new admin model
            var model = new Admin();

            // Admin ekleme view'ını döndür
            // Return admin creation view
            return View(model);
        }

        // --------------------------
        // CREATE - POST
        // Yeni admin ekleme işlemi - POST metodu
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        [ValidateAntiForgeryToken] // CSRF koruması için
        public ActionResult Create(Admin model, string password)
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

            // E-posta kontrolü - Aynı e-posta ile admin var mı?
            // Email check - Is there an admin with the same email?
            if (db.Admins.Any(a => a.Email == model.Email))
            {
                TempData["AdminErrorKey"] = "EmailExists";
                TempData["AdminError_tr"] = "Bu e-posta adresi zaten kullanılıyor!";
                TempData["AdminError_en"] = "This email address is already in use!";
                return View(model);
            }

            // Şifre kontrolü
            // Password check
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                TempData["AdminErrorKey"] = "PasswordInvalid";
                TempData["AdminError_tr"] = "Şifre en az 6 karakter olmalıdır!";
                TempData["AdminError_en"] = "Password must be at least 6 characters!";
                return View(model);
            }

            // Şifreyi hash'le
            // Hash the password
            model.Password = HashPassword(password);

            // Model validasyonu kontrolü
            // Check model validation
            if (!ModelState.IsValid)
            {
                // Form hatalıysa aynı hataları gösterir
                // If form is invalid, show same errors
                return View(model);
            }

            // Admin'i veritabanına ekle
            // Add admin to database
            db.Admins.Add(model);
            db.SaveChanges();

            // Başarı mesajı
            // Success message
            TempData["AdminSuccessKey"] = "AdminCreated";
            TempData["AdminSuccess_tr"] = "Admin başarıyla oluşturuldu.";
            TempData["AdminSuccess_en"] = "Admin created successfully.";

            // Admin listesine yönlendir
            // Redirect to admin list
            return RedirectToAction("Index");
        }

        // --------------------------
        // EDIT - GET
        // Admin düzenleme sayfası - GET metodu
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

            // Admin ID kontrolü
            // Check admin ID
            if (id == null)
            {
                // ID yoksa 400 Bad Request hatası döndür
                // If ID is null, return 400 Bad Request error
                return new HttpStatusCodeResult(400);
            }

            // Admin'i veritabanından bul
            // Find admin from database
            var admin = db.Admins.Find(id);

            // Admin bulunamadıysa 404 hatası döndür
            // If admin not found, return 404 error
            if (admin == null)
                return HttpNotFound();

            // Admin düzenleme view'ını döndür
            // Return admin edit view
            return View(admin);
        }

        // --------------------------
        // EDIT - POST
        // Admin düzenleme işlemi - POST metodu
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        [ValidateAntiForgeryToken] // CSRF koruması için
        public ActionResult Edit(Admin model, string password)
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

            // E-posta kontrolü - Başka bir admin aynı e-postayı kullanıyor mu?
            // Email check - Is another admin using the same email?
            var existingAdmin = db.Admins.FirstOrDefault(a => a.Email == model.Email && a.AdminId != model.AdminId);
            if (existingAdmin != null)
            {
                TempData["AdminErrorKey"] = "EmailExists";
                TempData["AdminError_tr"] = "Bu e-posta adresi zaten kullanılıyor!";
                TempData["AdminError_en"] = "This email address is already in use!";
                return View(model);
            }

            // Admin'i veritabanından bul
            // Find admin from database
            var admin = db.Admins.Find(model.AdminId);

            // Admin bulunamadıysa 404 hatası döndür
            // If admin not found, return 404 error
            if (admin == null)
                return HttpNotFound();

            // Şifre değiştirme kontrolü
            // Password change check
            if (!string.IsNullOrEmpty(password))
            {
                // Şifre uzunluk kontrolü
                // Password length check
                if (password.Length < 6)
                {
                    TempData["AdminErrorKey"] = "PasswordInvalid";
                    TempData["AdminError_tr"] = "Şifre en az 6 karakter olmalıdır!";
                    TempData["AdminError_en"] = "Password must be at least 6 characters!";
                    return View(model);
                }

                // Yeni şifreyi hash'le
                // Hash new password
                admin.Password = HashPassword(password);
            }

            // Admin bilgilerini güncelle
            // Update admin information
            admin.FullName = model.FullName;
            admin.Email = model.Email;
            db.SaveChanges();

            // Başarı mesajı
            // Success message
            TempData["AdminSuccessKey"] = "AdminUpdated";
            TempData["AdminSuccess_tr"] = "Admin başarıyla güncellendi.";
            TempData["AdminSuccess_en"] = "Admin updated successfully.";

            // Admin listesine yönlendir
            // Redirect to admin list
            return RedirectToAction("Index");
        }

        // --------------------------
        // DELETE - GET
        // Admin silme işlemi - GET metodu
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

            // Admin ID kontrolü
            // Check admin ID
            if (id == null)
            {
                // ID yoksa 400 Bad Request hatası döndür
                // If ID is null, return 400 Bad Request error
                return new HttpStatusCodeResult(400);
            }

            // Admin'i veritabanından bul
            // Find admin from database
            var admin = db.Admins.Find(id);

            // Admin bulunamadıysa 404 hatası döndür
            // If admin not found, return 404 error
            if (admin == null)
                return HttpNotFound();

            // Kendi hesabını silme kontrolü
            // Check if trying to delete own account
            int currentAdminId = (int)Session["AdminId"];
            if (admin.AdminId == currentAdminId)
            {
                TempData["AdminErrorKey"] = "CannotDeleteSelf";
                TempData["AdminError_tr"] = "Kendi hesabınızı silemezsiniz!";
                TempData["AdminError_en"] = "You cannot delete your own account!";
                return RedirectToAction("Index");
            }

            // Admin'i veritabanından sil
            // Delete admin from database
            db.Admins.Remove(admin);
            db.SaveChanges();

            // Başarı mesajı
            // Success message
            TempData["AdminSuccessKey"] = "AdminDeleted";
            TempData["AdminSuccess_tr"] = "Admin başarıyla silindi.";
            TempData["AdminSuccess_en"] = "Admin deleted successfully.";

            // Admin listesine yönlendir
            // Redirect to admin list
            return RedirectToAction("Index");
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

