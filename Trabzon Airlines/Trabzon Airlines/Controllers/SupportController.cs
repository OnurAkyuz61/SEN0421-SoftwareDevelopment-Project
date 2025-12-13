using System;
using System.Web.Mvc;
using Trabzon_Airlines.Data;   
using Trabzon_Airlines.Models; 

namespace Trabzon_Airlines.Controllers
{
    // Müşteri destek taleplerini işlemek için controller
    public class SupportController : Controller
    {
        private TAContext db = new TAContext();

        // DESTEK FORMU - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Support(string fullName, string email, string topic, string pnr, string message)
        {
            // Zorunlu alan kontrolü
            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(topic) ||
                string.IsNullOrWhiteSpace(message))
            {
                TempData["SupportError_tr"] = "Lütfen zorunlu alanları doldurun.";
                TempData["SupportError_en"] = "Please fill in all required fields.";

                // Form sayfası HomeController/Support olduğu için dön
                return RedirectToAction("Support", "Home");
            }

            // Destek talebi oluştur
            var support = new SupportRequest
            {
                FullName = fullName.Trim(),
                Email = email.Trim(),
                Topic = topic.Trim(),
                PNR = string.IsNullOrWhiteSpace(pnr) ? null : pnr.Trim().ToUpper(),
                Message = message.Trim()
            };

            // Kullanıcı giriş yaptıysa UserId bağla
            if (Session["UserId"] != null)
            {
                int uid;
                if (int.TryParse(Session["UserId"].ToString(), out uid))
                {
                    support.UserId = uid;
                }
            }

            // Veritabanına kaydet
            db.SupportRequests.Add(support);
            db.SaveChanges();

            // Başarı mesajları (TR / EN)
            TempData["SupportSuccess_tr"] = $"Destek talebiniz oluşturuldu. Talep numaranız: #{support.SupportRequestId}.";
            TempData["SupportSuccess_en"] = $"Your support request has been created. Ticket ID: #{support.SupportRequestId}.";

            // form sayfasına (Home/Support) dön
            return RedirectToAction("Support", "Home");
        }

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
