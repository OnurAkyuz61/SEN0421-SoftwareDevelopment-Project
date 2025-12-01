using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Trabzon_Airlines.Models;
using System.Security.Cryptography;
using System.Text;

namespace Trabzon_Airlines.Controllers
{
    public class HomeController : Controller
    {
        // Veritabanı bağlamı
        // Database context
        private TrabzonAirlinesDbContext db = new TrabzonAirlinesDbContext();

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        // Giriş sayfası - GET
        // Login page - GET
        [HttpGet]
        public ActionResult Login()
        {
            // Eğer zaten giriş yapılmışsa ana sayfaya yönlendir
            // If already logged in, redirect to homepage
            if (Session["UserId"] != null)
            {
                return RedirectToAction("Index");
            }
            return View();
        }

        // Giriş işlemi - POST
        // Login process - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password, bool? rememberMe)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "E-posta ve şifre gereklidir";
                return View();
            }

            // Şifreyi hash'le
            // Hash the password
            string hashedPassword = HashPassword(password);

            // Kullanıcıyı veritabanından bul
            // Find user from database
            var user = db.Users.FirstOrDefault(u => u.Email == email && u.Password == hashedPassword && u.IsActive);

            if (user != null)
            {
                // Giriş başarılı - Session'a kaydet
                // Login successful - Save to session
                Session["UserId"] = user.Id;
                Session["UserEmail"] = user.Email;
                Session["UserName"] = $"{user.FirstName} {user.LastName}";

                // Beni hatırla seçildiyse cookie'ye kaydet
                // If remember me is selected, save to cookie
                if (rememberMe == true)
                {
                    HttpCookie cookie = new HttpCookie("RememberMe", user.Id.ToString());
                    cookie.Expires = DateTime.Now.AddDays(30);
                    Response.Cookies.Add(cookie);
                }

                return RedirectToAction("Index");
            }
            else
            {
                ViewBag.Error = "E-posta veya şifre hatalı";
                return View();
            }
        }

        // Üye ol sayfası - GET
        // Signup page - GET
        [HttpGet]
        public ActionResult Signup()
        {
            // Eğer zaten giriş yapılmışsa ana sayfaya yönlendir
            // If already logged in, redirect to homepage
            if (Session["UserId"] != null)
            {
                return RedirectToAction("Index");
            }
            return View();
        }

        // Üye ol işlemi - POST
        // Signup process - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Signup(User user, string confirmPassword, bool? marketing, bool? kvkk)
        {
            // Şifre kontrolü
            // Password check
            if (user.Password != confirmPassword)
            {
                ViewBag.Error = "Şifreler eşleşmiyor";
                return View(user);
            }

            // E-posta kontrolü
            // Email check
            if (db.Users.Any(u => u.Email == user.Email))
            {
                ViewBag.Error = "Bu e-posta adresi zaten kullanılıyor";
                return View(user);
            }

            // KVKK onayı kontrolü
            // KVKK consent check
            if (kvkk != true)
            {
                ViewBag.Error = "KVKK onayı gereklidir";
                return View(user);
            }

            if (ModelState.IsValid)
            {
                // Şifreyi hash'le
                // Hash the password
                user.Password = HashPassword(user.Password);

                // Varsayılan değerler
                // Default values
                user.CreatedAt = DateTime.Now;
                user.IsActive = true;
                user.MarketingConsent = marketing ?? false;
                user.KvkkConsent = kvkk ?? false;

                // TC vatandaşı değilse TC numarasını temizle
                // If not Turkish citizen, clear TC number
                if (user.NotTurkishCitizen)
                {
                    user.TcNumber = null;
                }

                // Veritabanına ekle
                // Add to database
                db.Users.Add(user);
                db.SaveChanges();

                // Otomatik giriş yap
                // Auto login
                Session["UserId"] = user.Id;
                Session["UserEmail"] = user.Email;
                Session["UserName"] = $"{user.FirstName} {user.LastName}";

                return RedirectToAction("Index");
            }

            return View(user);
        }

        // Çıkış işlemi
        // Logout process
        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            
            // Remember me cookie'sini sil
            // Delete remember me cookie
            if (Request.Cookies["RememberMe"] != null)
            {
                HttpCookie cookie = new HttpCookie("RememberMe");
                cookie.Expires = DateTime.Now.AddDays(-1);
                Response.Cookies.Add(cookie);
            }

            return RedirectToAction("Index");
        }

        // Paketler sayfası - Veritabanından paketleri getir
        // Packages page - Get packages from database
        public ActionResult Packages()
        {
            var packages = db.Packages.Where(p => p.IsActive).OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt).ToList();
            return View(packages);
        }

        // Paket detay sayfası
        // Package detail page
        public ActionResult PackageDetail(int id)
        {
            var package = db.Packages.Find(id);
            if (package == null || !package.IsActive)
            {
                return HttpNotFound();
            }
            return View(package);
        }

        // Fırsatlar sayfası - Veritabanından fırsatları getir
        // Deals page - Get deals from database
        public ActionResult Deals()
        {
            var deals = db.Deals.Where(d => d.IsActive && d.EndDate >= DateTime.Now).OrderByDescending(d => d.IsFeatured).ThenByDescending(d => d.CreatedAt).ToList();
            return View(deals);
        }

        // Fırsat detay sayfası
        // Deal detail page
        public ActionResult DealDetail(int id)
        {
            var deal = db.Deals.Find(id);
            if (deal == null || !deal.IsActive || deal.EndDate < DateTime.Now)
            {
                return HttpNotFound();
            }
            return View(deal);
        }

        // Kullanıcı ayarları sayfası - GET
        // User settings page - GET
        [HttpGet]
        public ActionResult Settings()
        {
            // Giriş kontrolü
            // Login check
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login");
            }

            int userId = (int)Session["UserId"];
            var user = db.Users.Find(userId);
            if (user == null)
            {
                return HttpNotFound();
            }

            return View(user);
        }

        // Kullanıcı ayarları güncelleme - POST
        // Update user settings - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Settings(User updatedUser, string currentPassword, string newPassword, string confirmPassword)
        {
            // Giriş kontrolü
            // Login check
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login");
            }

            int userId = (int)Session["UserId"];
            var user = db.Users.Find(userId);
            if (user == null)
            {
                return HttpNotFound();
            }

            // Şifre değiştirme kontrolü
            // Password change check
            if (!string.IsNullOrEmpty(newPassword))
            {
                // Mevcut şifre kontrolü
                // Current password check
                string hashedCurrentPassword = HashPassword(currentPassword);
                if (user.Password != hashedCurrentPassword)
                {
                    ViewBag.Error = "Mevcut şifre hatalı";
                    return View(user);
                }

                // Yeni şifre kontrolü
                // New password check
                if (newPassword != confirmPassword)
                {
                    ViewBag.Error = "Yeni şifreler eşleşmiyor";
                    return View(user);
                }

                // Şifreyi güncelle
                // Update password
                user.Password = HashPassword(newPassword);
            }

            // E-posta değişikliği kontrolü
            // Email change check
            if (user.Email != updatedUser.Email)
            {
                if (db.Users.Any(u => u.Email == updatedUser.Email && u.Id != userId))
                {
                    ViewBag.Error = "Bu e-posta adresi zaten kullanılıyor";
                    return View(user);
                }
                user.Email = updatedUser.Email;
            }

            // Diğer bilgileri güncelle
            // Update other information
            user.FirstName = updatedUser.FirstName;
            user.LastName = updatedUser.LastName;
            user.Phone = updatedUser.Phone;
            user.BirthDate = updatedUser.BirthDate;
            user.Gender = updatedUser.Gender;
            user.MarketingConsent = updatedUser.MarketingConsent;
            user.UpdatedAt = DateTime.Now;

            // TC vatandaşı değilse TC numarasını temizle
            // If not Turkish citizen, clear TC number
            if (updatedUser.NotTurkishCitizen)
            {
                user.TcNumber = null;
            }
            else
            {
                user.TcNumber = updatedUser.TcNumber;
            }

            db.SaveChanges();

            // Session'ı güncelle
            // Update session
            Session["UserEmail"] = user.Email;
            Session["UserName"] = $"{user.FirstName} {user.LastName}";

            ViewBag.Success = "Ayarlarınız başarıyla güncellendi";
            return View(user);
        }

        // Sitemap sayfası
        // Sitemap page
        public ActionResult Sitemap()
        {
            return View();
        }

        // Gizlilik Politikası sayfası
        // Privacy Policy page
        public ActionResult Privacy()
        {
            return View();
        }

        // Şifre hash'leme fonksiyonu - SHA256
        // Password hashing function - SHA256
        private string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        // Dispose - Kaynakları temizle
        // Dispose - Clean up resources
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}