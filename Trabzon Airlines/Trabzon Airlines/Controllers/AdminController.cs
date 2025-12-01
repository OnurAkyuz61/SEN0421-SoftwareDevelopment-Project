using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Trabzon_Airlines.Models;
using Trabzon_Airlines.Attributes;
using System.Security.Cryptography;
using System.Text;
using System.Web.Security;

namespace Trabzon_Airlines.Controllers
{
    // Admin Controller - Yönetici paneli için
    // Admin Controller - For administrator panel
    public class AdminController : Controller
    {
        // Veritabanı bağlamı
        // Database context
        private TrabzonAirlinesDbContext db = new TrabzonAirlinesDbContext();

        // Admin giriş sayfası - GET
        // Admin login page - GET
        [HttpGet]
        public ActionResult Login()
        {
            // Eğer zaten giriş yapılmışsa dashboard'a yönlendir
            // If already logged in, redirect to dashboard
            if (Session["AdminId"] != null)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        // Admin giriş işlemi - POST
        // Admin login process - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Kullanıcı adı ve şifre gereklidir";
                return View();
            }

            // Şifreyi hash'le
            // Hash the password
            string hashedPassword = HashPassword(password);

            // Admin'i veritabanından bul
            // Find admin from database
            var admin = db.Admins.FirstOrDefault(a => a.Username == username && a.Password == hashedPassword && a.IsActive);

            if (admin != null)
            {
                // Giriş başarılı - Session'a kaydet
                // Login successful - Save to session
                Session["AdminId"] = admin.Id;
                Session["AdminUsername"] = admin.Username;
                Session["IsSuperAdmin"] = admin.IsSuperAdmin;

                // Son giriş tarihini güncelle
                // Update last login date
                admin.LastLoginDate = DateTime.Now;
                db.SaveChanges();

                return RedirectToAction("Dashboard");
            }
            else
            {
                ViewBag.Error = "Kullanıcı adı veya şifre hatalı";
                return View();
            }
        }

        // Admin çıkış işlemi
        // Admin logout process
        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login");
        }

        // Admin dashboard - Ana sayfa
        // Admin dashboard - Main page
        [AuthorizeAdmin] // Özel yetkilendirme attribute'u
        public ActionResult Dashboard()
        {
            // İstatistikler
            // Statistics
            ViewBag.TotalPackages = db.Packages.Count();
            ViewBag.ActivePackages = db.Packages.Count(p => p.IsActive);
            ViewBag.TotalDeals = db.Deals.Count();
            ViewBag.ActiveDeals = db.Deals.Count(d => d.IsActive);
            ViewBag.TotalUsers = db.Users.Count();
            ViewBag.TotalReservations = db.PackageReservations.Count() + db.DealReservations.Count();

            return View();
        }

        // Paketler listesi
        // Packages list
        [AuthorizeAdmin]
        public ActionResult Packages()
        {
            var packages = db.Packages.OrderByDescending(p => p.CreatedAt).ToList();
            return View(packages);
        }

        // Paket ekleme sayfası - GET
        // Add package page - GET
        [AuthorizeAdmin]
        [HttpGet]
        public ActionResult AddPackage()
        {
            return View();
        }

        // Paket ekleme işlemi - POST
        // Add package process - POST
        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddPackage(Package package)
        {
            if (ModelState.IsValid)
            {
                package.CreatedAt = DateTime.Now;
                package.IsActive = true;
                db.Packages.Add(package);
                db.SaveChanges();
                return RedirectToAction("Packages");
            }
            return View(package);
        }

        // Paket düzenleme sayfası - GET
        // Edit package page - GET
        [AuthorizeAdmin]
        [HttpGet]
        public ActionResult EditPackage(int id)
        {
            var package = db.Packages.Find(id);
            if (package == null)
            {
                return HttpNotFound();
            }
            return View(package);
        }

        // Paket düzenleme işlemi - POST
        // Edit package process - POST
        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditPackage(Package package)
        {
            if (ModelState.IsValid)
            {
                var existingPackage = db.Packages.Find(package.Id);
                if (existingPackage != null)
                {
                    existingPackage.Title = package.Title;
                    existingPackage.Description = package.Description;
                    existingPackage.ShortDescription = package.ShortDescription;
                    existingPackage.ImageUrl = package.ImageUrl;
                    existingPackage.Price = package.Price;
                    existingPackage.OldPrice = package.OldPrice;
                    existingPackage.DiscountPercentage = package.DiscountPercentage;
                    existingPackage.Category = package.Category;
                    existingPackage.SubCategory = package.SubCategory;
                    existingPackage.Duration = package.Duration;
                    existingPackage.Nights = package.Nights;
                    existingPackage.Rating = package.Rating;
                    existingPackage.Badge = package.Badge;
                    existingPackage.Features = package.Features;
                    existingPackage.HotelStars = package.HotelStars;
                    existingPackage.FlightIncluded = package.FlightIncluded;
                    existingPackage.MealType = package.MealType;
                    existingPackage.IsActive = package.IsActive;
                    existingPackage.IsFeatured = package.IsFeatured;
                    existingPackage.UpdatedAt = DateTime.Now;
                    db.SaveChanges();
                    return RedirectToAction("Packages");
                }
            }
            return View(package);
        }

        // Paket silme işlemi
        // Delete package process
        [AuthorizeAdmin]
        public ActionResult DeletePackage(int id)
        {
            var package = db.Packages.Find(id);
            if (package != null)
            {
                db.Packages.Remove(package);
                db.SaveChanges();
            }
            return RedirectToAction("Packages");
        }

        // Fırsatlar listesi
        // Deals list
        [AuthorizeAdmin]
        public ActionResult Deals()
        {
            var deals = db.Deals.OrderByDescending(d => d.CreatedAt).ToList();
            return View(deals);
        }

        // Fırsat ekleme sayfası - GET
        // Add deal page - GET
        [AuthorizeAdmin]
        [HttpGet]
        public ActionResult AddDeal()
        {
            return View();
        }

        // Fırsat ekleme işlemi - POST
        // Add deal process - POST
        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddDeal(Deal deal)
        {
            if (ModelState.IsValid)
            {
                deal.CreatedAt = DateTime.Now;
                deal.IsActive = true;
                db.Deals.Add(deal);
                db.SaveChanges();
                return RedirectToAction("Deals");
            }
            return View(deal);
        }

        // Fırsat düzenleme sayfası - GET
        // Edit deal page - GET
        [AuthorizeAdmin]
        [HttpGet]
        public ActionResult EditDeal(int id)
        {
            var deal = db.Deals.Find(id);
            if (deal == null)
            {
                return HttpNotFound();
            }
            return View(deal);
        }

        // Fırsat düzenleme işlemi - POST
        // Edit deal process - POST
        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditDeal(Deal deal)
        {
            if (ModelState.IsValid)
            {
                var existingDeal = db.Deals.Find(deal.Id);
                if (existingDeal != null)
                {
                    existingDeal.Title = deal.Title;
                    existingDeal.Description = deal.Description;
                    existingDeal.ShortDescription = deal.ShortDescription;
                    existingDeal.ImageUrl = deal.ImageUrl;
                    existingDeal.DiscountPercentage = deal.DiscountPercentage;
                    existingDeal.Badge = deal.Badge;
                    existingDeal.CampaignCode = deal.CampaignCode;
                    existingDeal.StartDate = deal.StartDate;
                    existingDeal.EndDate = deal.EndDate;
                    existingDeal.MinPersonCount = deal.MinPersonCount;
                    existingDeal.MaxPersonCount = deal.MaxPersonCount;
                    existingDeal.IsStudentDiscount = deal.IsStudentDiscount;
                    existingDeal.IsFamilyDiscount = deal.IsFamilyDiscount;
                    existingDeal.IsGroupDiscount = deal.IsGroupDiscount;
                    existingDeal.IsActive = deal.IsActive;
                    existingDeal.IsFeatured = deal.IsFeatured;
                    existingDeal.UpdatedAt = DateTime.Now;
                    db.SaveChanges();
                    return RedirectToAction("Deals");
                }
            }
            return View(deal);
        }

        // Fırsat silme işlemi
        // Delete deal process
        [AuthorizeAdmin]
        public ActionResult DeleteDeal(int id)
        {
            var deal = db.Deals.Find(id);
            if (deal != null)
            {
                db.Deals.Remove(deal);
                db.SaveChanges();
            }
            return RedirectToAction("Deals");
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

