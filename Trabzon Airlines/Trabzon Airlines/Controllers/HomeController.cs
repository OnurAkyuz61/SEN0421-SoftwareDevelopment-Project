using System; // DateTime için
using System.Collections.Generic; // Koleksiyonlar için
using System.Linq; // LINQ sorguları için
using System.Web; // HTTP context için
using System.Web.Mvc; // MVC framework için
using Trabzon_Airlines.Data;   // TAContext için
using Trabzon_Airlines.Models; // SupportRequest için


namespace Trabzon_Airlines.Controllers
{
    // Ana sayfa controller'ı - Genel sayfa yönlendirmeleri
    // Home page controller - General page redirects
    public class HomeController : Controller
    {
        // Destek taleplerini listelemek için DB context
        private TAContext db = new TAContext();

        // --------------------------
        // INDEX - GET
        // Ana sayfa - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Index()
        {
            // Ana sayfa view'ını döndür
            // Return homepage view
            return View();
        }

        // --------------------------
        // ABOUT - GET
        // Hakkımızda sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult About()
        {
            // Hakkımızda view'ını döndür
            // Return about view
            return View();
        }

        // --------------------------
        // CONTACT - GET
        // İletişim sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Contact()
        {
            // İletişim view'ını döndür
            // Return contact view
            return View();
        }

        // --------------------------
        // CONTACT - POST
        // İletişim formu gönderimi - POST metodu
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        [ValidateAntiForgeryToken] // CSRF koruması - CSRF protection
        public ActionResult Contact(string fullName, string email, string phone, string subject, string reservationNumber, string message, string kvkk)
        {
            // Zorunlu alan kontrolü - Required field check
            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(subject) ||
                string.IsNullOrWhiteSpace(message) ||
                string.IsNullOrWhiteSpace(kvkk))
            {
                TempData["ContactError_tr"] = "Lütfen tüm zorunlu alanları doldurun.";
                TempData["ContactError_en"] = "Please fill in all required fields.";
                return RedirectToAction("Contact");
            }

            try
            {
                // E-posta gönderimi - Email sending
                System.Net.Mail.MailMessage mail = new System.Net.Mail.MailMessage();
                mail.From = new System.Net.Mail.MailAddress("trabzonairlines@gmail.com", "Trabzon Airlines İletişim Formu");
                mail.To.Add("trabzonairlines@gmail.com");
                mail.Subject = $"İletişim Formu - {subject}";
                mail.IsBodyHtml = true;
                mail.Body = $@"
                    <h2>Yeni İletişim Formu Mesajı</h2>
                    <p><strong>Ad Soyad:</strong> {fullName}</p>
                    <p><strong>E-posta:</strong> {email}</p>
                    <p><strong>Telefon:</strong> {(string.IsNullOrWhiteSpace(phone) ? "Belirtilmemiş" : phone)}</p>
                    <p><strong>Konu:</strong> {subject}</p>
                    <p><strong>Rezervasyon Numarası:</strong> {(string.IsNullOrWhiteSpace(reservationNumber) ? "Yok" : reservationNumber)}</p>
                    <p><strong>Mesaj:</strong></p>
                    <p>{message.Replace("\n", "<br>")}</p>
                    <hr>
                    <p><small>Bu mesaj {DateTime.Now:dd.MM.yyyy HH:mm} tarihinde web sitesi üzerinden gönderilmiştir.</small></p>
                ";

                // SMTP ayarları - SMTP settings (Manuel ayarlar - Manual settings)
                System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587);
                smtp.EnableSsl = true;
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new System.Net.NetworkCredential("trabzonairlines@gmail.com", "xnzrqqjippyamtsn");
                smtp.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
                smtp.Timeout = 30000; // 30 saniye timeout - 30 second timeout

                smtp.Send(mail);

                // Başarı mesajı - Success message
                TempData["ContactSuccess_tr"] = "Mesajınız başarıyla gönderildi! En kısa sürede size dönüş yapacağız.";
                TempData["ContactSuccess_en"] = "Your message has been sent successfully! We will get back to you soon.";
            }
            catch (System.Net.Mail.SmtpException smtpEx)
            {
                // SMTP hatası - SMTP error
                string errorMsg = $"SMTP Hatası: {smtpEx.Message}";
                if (smtpEx.InnerException != null)
                {
                    errorMsg += $" | İç Hata: {smtpEx.InnerException.Message}";
                }
                System.Diagnostics.Debug.WriteLine(errorMsg);
                
                TempData["ContactError_tr"] = "E-posta gönderilirken bir hata oluştu. Lütfen daha sonra tekrar deneyin veya doğrudan trabzonairlines@gmail.com adresine e-posta gönderin.";
                TempData["ContactError_en"] = "An error occurred while sending the email. Please try again later or send an email directly to trabzonairlines@gmail.com.";
            }
            catch (Exception ex)
            {
                // Genel hata - General error
                string errorMsg = $"Hata: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMsg += $" | İç Hata: {ex.InnerException.Message}";
                }
                System.Diagnostics.Debug.WriteLine($"Contact form error: {errorMsg}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                
                TempData["ContactError_tr"] = "Mesaj gönderilirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                TempData["ContactError_en"] = "An error occurred while sending the message. Please try again later.";
            }

            return RedirectToAction("Contact");
        }

        //Destek sayfası - GET
        [HttpGet]
        public ActionResult Support()
        {
            ViewBag.Title = "Destek";
            ViewBag.TitleEn = "Support";

            // Varsayılan
            ViewBag.IsLoggedIn = false;
            ViewBag.MySupportTickets = null;

            // Kullanıcı login ise kendi taleplerini çek
            if (Session["UserId"] != null)
            {
                int userId;
                if (int.TryParse(Session["UserId"].ToString(), out userId))
                {
                    var myTickets = db.SupportRequests
                                      .Where(x => x.UserId == userId)
                                      .OrderByDescending(x => x.CreatedAt)
                                      .Take(10)
                                      .ToList();

                    ViewBag.MySupportTickets = myTickets;
                }

                ViewBag.IsLoggedIn = true;
            }

            return View();
        }

        // --------------------------
        // LOGIN - GET
        // Giriş sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Login()
        {
            // Giriş view'ını döndür
            // Return login view
            return View();
        }

        // --------------------------
        // SIGNUP - GET
        // Kayıt sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Signup()
        {
            // Kayıt view'ını döndür
            // Return signup view
            return View();
        }

        // --------------------------
        // TRIP AI - GET
        // Seyahat AI sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult TripAI()
        {
            // Trip AI view'ını döndür
            // Return Trip AI view
            return View();  
        }

        // --------------------------
        // PRIVACY - GET
        // Gizlilik Politikası sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Privacy()
        {
            // Gizlilik politikası view'ını döndür
            // Return privacy policy view
            return View();
        }

        // --------------------------
        // KVKK - GET
        // KVKK Aydınlatma Metni sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult KVKK()
        {
            // KVKK aydınlatma metni view'ını döndür
            // Return KVKK clarification text view
            return View();
        }

        // --------------------------
        // SITEMAP - GET
        // Site haritası sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Sitemap()
        {
            // Site haritası view'ını döndür
            // Return sitemap view
            return View();
        }
    }

}
