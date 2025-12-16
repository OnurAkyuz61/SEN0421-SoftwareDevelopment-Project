using System;
using System.Linq;
using System.Web.Mvc;
using System.Security.Cryptography;
using System.Text;
using Trabzon_Airlines.Data;
using Trabzon_Airlines.Models;

namespace Trabzon_Airlines.Controllers
{
    // Hesap yönetimi controller'ı - Kullanıcı kayıt ve giriş işlemleri
    public class AccountController : Controller
    {
        private TAContext db = new TAContext();

        // --------------------------
        // SIGNUP (REGISTER) - POST
        // --------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Signup(User model)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction("Signup", "Home");
            }

            var existing = db.Users.FirstOrDefault(x => x.Email == model.Email);
            if (existing != null)
            {
                TempData["Error_tr"] = "Bu e-posta adresi zaten kayıtlı!";
                TempData["Error_en"] = "This email address is already registered!";
                return RedirectToAction("Signup", "Home");
            }

            model.Password = HashPassword(model.Password);

            db.Users.Add(model);
            db.SaveChanges();

            TempData["Success_tr"] = "Kayıt başarılı! Giriş yapabilirsiniz.";
            TempData["Success_en"] = "Registration successful! You can now log in.";

            return RedirectToAction("Login", "Home");
        }

        // --------------------------
        // LOGIN - POST
        // --------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Login(string email, string password)
        {
            // returnUrl'i metodun başında bir kez al (hem QueryString'den hem de form'dan)
            // Get returnUrl once at the beginning of the method (from both QueryString and form)
            string returnUrl = Request.Form["returnUrl"] ?? Request.QueryString["returnUrl"];

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["Error_tr"] = "E-posta ve şifre gereklidir!";
                TempData["Error_en"] = "Email and password are required!";
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    return RedirectToAction("Login", "Home", new { returnUrl = returnUrl });
                }
                return RedirectToAction("Login", "Home");
            }

            string hashedPassword = HashPassword(password);

            var userByEmail = db.Users.FirstOrDefault(x => x.Email == email);
            var user = db.Users.FirstOrDefault(x => x.Email == email && x.Password == hashedPassword);

            if (user == null)
            {
                if (userByEmail != null)
                {
                    TempData["Error_tr"] = "Şifre hatalı!";
                    TempData["Error_en"] = "Incorrect password!";
                }
                else
                {
                    TempData["Error_tr"] = "Bu e-posta adresi kayıtlı değil! Lütfen önce kayıt olun.";
                    TempData["Error_en"] = "This email address is not registered! Please sign up first.";
                }
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    return RedirectToAction("Login", "Home", new { returnUrl = returnUrl });
                }
                return RedirectToAction("Login", "Home");
            }

            // Session set
            Session["UserId"] = user.UserId;
            Session["UserName"] = user.FirstName;
            Session["UserEmail"] = user.Email;
            
            // returnUrl varsa ve geçerliyse oraya yönlendir
            // If returnUrl exists and is valid, redirect there
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // Kullanıcı paneline
            return RedirectToAction("Dashboard", "Account");
        }

        // --------------------------
        // KULLANICI DASHBOARD
        // --------------------------
        [HttpGet]
        public ActionResult Dashboard()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Home");
            }

            int userId = (int)Session["UserId"];
            var user = db.Users.Find(userId);

            if (user == null)
            {
                Session.Clear();
                return RedirectToAction("Login", "Home");
            }

            var fullName = user.FirstName + " " + user.LastName;
            ViewBag.UserFullName = fullName;

            // Booking tablosunda UserId yok → PassengerName/Surname ile eşliyoruz
            var bookingsQuery =
                db.Bookings
                  .Where(b => b.PassengerName == user.FirstName &&
                              b.PassengerSurname == user.LastName)
                  .Join(db.Flights,
                        b => b.FlightId,
                        f => f.FlightId,
                        (b, f) => new { Booking = b, Flight = f });

            var now = DateTime.Now;

            var upcomingList = bookingsQuery
                .Where(x => x.Flight.DepartureTime >= now)
                .OrderBy(x => x.Flight.DepartureTime)
                .ToList()
                .Select(x => new BookingListItemViewModel
                {
                    BookingId = x.Booking.BookingId,
                    PNR = x.Booking.PNR,
                    FlightNumber = x.Flight.FlightNumber,
                    FromCode = x.Flight.FromCode,
                    ToCode = x.Flight.ToCode,
                    DepartureTime = x.Flight.DepartureTime,
                    ArrivalTime = x.Flight.ArrivalTime,
                    Price = x.Booking.Price  //Dashborddan alınan fiyat çıkar
                })
                .ToList();

            var pastList = bookingsQuery
                .Where(x => x.Flight.DepartureTime < now)
                .OrderByDescending(x => x.Flight.DepartureTime)
                .ToList()
                .Select(x => new BookingListItemViewModel
                {
                    BookingId = x.Booking.BookingId,
                    PNR = x.Booking.PNR,
                    FlightNumber = x.Flight.FlightNumber,
                    FromCode = x.Flight.FromCode,
                    ToCode = x.Flight.ToCode,
                    DepartureTime = x.Flight.DepartureTime,
                    ArrivalTime = x.Flight.ArrivalTime,
                    Price = x.Booking.Price
                })
                .ToList();

            var allBookings = bookingsQuery.ToList();
            var totalSpent = allBookings.Sum(x => x.Booking.Price);

            var vm = new UserDashboardViewModel
            {
                FullName = fullName,
                TotalBookings = allBookings.Count,
                UpcomingCount = upcomingList.Count,
                PastCount = pastList.Count,
                TotalSpent = totalSpent,
                UpcomingBookings = upcomingList,
                PastBookings = pastList
            };

            return View(vm);
        }

        // --------------------------
        // REZERVASYON İPTAL
        // --------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelBooking(int id)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Home");
            }

            int userId = (int)Session["UserId"];
            var user = db.Users.Find(userId);

            if (user == null)
            {
                Session.Clear();
                return RedirectToAction("Login", "Home");
            }

            var booking = db.Bookings.FirstOrDefault(b =>
                b.BookingId == id &&
                b.PassengerName == user.FirstName &&
                b.PassengerSurname == user.LastName);

            if (booking == null)
            {
                TempData["Error_tr"] = "Rezervasyon bulunamadı.";
                TempData["Error_en"] = "Booking not found.";
                return RedirectToAction("Dashboard");
            }

            // Koltuğu boşa çıkar
            var flightSeat = db.FlightSeats
                .FirstOrDefault(fs => fs.FlightId == booking.FlightId &&
                                      fs.SeatId == booking.SeatId);

            if (flightSeat != null)
            {
                flightSeat.IsReserved = false;
            }

            db.Bookings.Remove(booking);
            db.SaveChanges();

            TempData["Success_tr"] = "Rezervasyonunuz iptal edildi.";
            TempData["Success_en"] = "Your booking has been cancelled.";
            return RedirectToAction("Dashboard");
        }

        // --------------------------
        // HESABIM (GET)
        // --------------------------
        [HttpGet]
        public ActionResult Account()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Home");
            }

            int userId = (int)Session["UserId"];
            var user = db.Users.Find(userId);

            if (user == null)
            {
                Session.Clear();
                return RedirectToAction("Login", "Home");
            }

            var vm = new AccountViewModel
            {
                UserId = user.UserId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                TcNumber = user.TcNumber,
                BirthDate = user.BirthDate,
                Gender = user.Gender,
                ChangePassword = new ChangePasswordViewModel()
            };

            return View(vm);
        }

        // --------------------------
        // HESABIM (POST) - Şifre değiştir
        // --------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Account(AccountViewModel model)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Home");
            }

            int userId = (int)Session["UserId"];
            var user = db.Users.Find(userId);

            if (user == null)
            {
                Session.Clear();
                return RedirectToAction("Login", "Home");
            }

            if (model.ChangePassword != null &&
                !string.IsNullOrWhiteSpace(model.ChangePassword.CurrentPassword) &&
                !string.IsNullOrWhiteSpace(model.ChangePassword.NewPassword))
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var currentHashed = HashPassword(model.ChangePassword.CurrentPassword);

                if (user.Password != currentHashed)
                {
                    ModelState.AddModelError("", "Mevcut şifre hatalı.");
                    return View(model);
                }

                user.Password = HashPassword(model.ChangePassword.NewPassword);
                db.SaveChanges();

                TempData["Success_tr"] = "Şifreniz başarıyla güncellendi.";
                TempData["Success_en"] = "Your password has been updated.";
                return RedirectToAction("Account");
            }

            return RedirectToAction("Account");
        }

        // --------------------------
        // LOGOUT - Çıkış
        // --------------------------
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // --------------------------
        // ŞİFRE HASH FONKSİYONU
        // --------------------------
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

        // --------------------------
        // DISPOSE
        // --------------------------
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
