using System; // DateTime için
using System.Linq; // LINQ sorguları için
using System.Web.Mvc; // MVC controller için
using Trabzon_Airlines.Data; // Veritabanı bağlamı için
using Trabzon_Airlines.Models; // Model sınıfları için
using System.Data.Entity; // Entity Framework için (Include metodu için)

namespace Trabzon_Airlines.Controllers
{
    // Admin Kullanıcı Yönetimi Controller'ı - Admin panelinde kullanıcı yönetimi
    // Admin Users Management Controller - User management in admin panel
    public class AdminUsersController : Controller
    {
        // Veritabanı bağlamı - TAContext sınıfından instance
        // Database context - Instance from TAContext class
        private TAContext db = new TAContext();

        // --------------------------
        // INDEX - GET
        // Kullanıcı listesi - GET metodu
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

            // Tüm kullanıcıları veritabanından getir
            // Get all users from database
            var users = db.Users
                .OrderByDescending(u => u.UserId) // ID'ye göre azalan sıralama
                .ToList();

            // Kullanıcı listesi view'ını döndür
            // Return user list view
            return View(users);
        }

        // --------------------------
        // DETAILS - GET
        // Kullanıcı detayı - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Details(int? id)
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

            // Kullanıcı ID kontrolü
            // Check user ID
            if (id == null)
            {
                // ID yoksa 400 Bad Request hatası döndür
                // If ID is null, return 400 Bad Request error
                return new HttpStatusCodeResult(400);
            }

            // Kullanıcıyı veritabanından bul
            // Find user from database
            var user = db.Users.Find(id);

            // Kullanıcı bulunamadıysa 404 hatası döndür
            // If user not found, return 404 error
            if (user == null)
                return HttpNotFound();

            // Kullanıcının rezervasyonlarını getir
            // Get user's bookings
            var bookings = db.Bookings
                .Where(b => b.PassengerName == user.FirstName && b.PassengerSurname == user.LastName)
                .Include(b => b.Flight) // Uçuş bilgisini dahil et
                .Include(b => b.Seat) // Koltuk bilgisini dahil et
                .ToList();

            // ViewBag'e rezervasyonları ekle
            // Add bookings to ViewBag
            ViewBag.Bookings = bookings;

            // Kullanıcı detay view'ını döndür
            // Return user detail view
            return View(user);
        }

        // --------------------------
        // DELETE - GET
        // Kullanıcı silme işlemi - GET metodu
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

            // Kullanıcı ID kontrolü
            // Check user ID
            if (id == null)
            {
                // ID yoksa 400 Bad Request hatası döndür
                // If ID is null, return 400 Bad Request error
                return new HttpStatusCodeResult(400);
            }

            // Kullanıcıyı veritabanından bul
            // Find user from database
            var user = db.Users.Find(id);

            // Kullanıcı bulunamadıysa 404 hatası döndür
            // If user not found, return 404 error
            if (user == null)
                return HttpNotFound();

            // Kullanıcıyı veritabanından sil
            // Delete user from database
            db.Users.Remove(user);
            db.SaveChanges(); // Değişiklikleri kaydet

            // Başarı mesajı
            // Success message
            TempData["AdminSuccessKey"] = "UserDeleted";
            TempData["AdminSuccess_tr"] = "Kullanıcı başarıyla silindi.";
            TempData["AdminSuccess_en"] = "User deleted successfully.";

            // Kullanıcı listesine yönlendir
            // Redirect to user list
            return RedirectToAction("Index");
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

