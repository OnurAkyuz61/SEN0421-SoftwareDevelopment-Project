using System;
using System.Linq;
using System.Web.Mvc;
using Trabzon_Airlines.Data;
using Trabzon_Airlines.Models;

namespace Trabzon_Airlines.Controllers
{
    // Admin panelde destek taleplerini yönetmek için
    public class AdminSupportController : Controller
    {
        private TAContext db = new TAContext();

        // Admin login kontrolü 
        private bool IsAdminLogged()
        {
            return Session["AdminId"] != null;
        }

        private ActionResult RedirectToAdminLogin()
        {
            return RedirectToAction("Login", "Admin");
        }

        // DESTEK TALEPLERİ LİSTESİ
        public ActionResult Index(string status = "all")
        {
            if (!IsAdminLogged())
                return RedirectToAdminLogin();

            var query = db.SupportRequests.AsQueryable();

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(x => x.Status == status);
            }

            var list = query
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            ViewBag.SelectedStatus = status;
            ViewBag.Title = "Destek Talepleri";
            ViewBag.TitleEn = "Support Tickets";

            return View(list);
        }

        // DURUM GÜNCELLEME
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(int id, string status, string returnStatus = "all")
        {
            if (!IsAdminLogged())
                return RedirectToAdminLogin();

            var ticket = db.SupportRequests.Find(id);
            if (ticket == null)
                return HttpNotFound();

            ticket.Status = status;
            ticket.UpdatedAt = DateTime.Now;
            db.SaveChanges();

            TempData["SupportAdminSuccess"] =
                $"Talep #{ticket.SupportRequestId} durumu '{status}' olarak güncellendi.";

            return RedirectToAction("Index", new { status = returnStatus });
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
