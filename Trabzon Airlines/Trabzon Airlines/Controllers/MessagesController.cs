using System;
using System.Linq;
using System.Web.Mvc;
using Trabzon_Airlines.Data;
using Trabzon_Airlines.Services;

namespace Trabzon_Airlines.Controllers
{
    // Kullanıcı mesaj kutusu (fiyat düştü bildirimleri vb.)
    // User inbox (price drop alerts etc.)
    public class MessagesController : Controller
    {
        private TAContext db = new TAContext();

        private int? CurrentUserId
        {
            get
            {
                int id;
                return Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out id) ? id : (int?)null;
            }
        }

        // --------------------------
        // INDEX - GET  (/Messages)
        // --------------------------
        [HttpGet]
        public ActionResult Index()
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Home", new { returnUrl = "/Messages" });

            // Takip edilen uçuşlarda yeni düşüş var mı?
            PriceWatchService.SyncUser(db, uid.Value);

            var messages = db.UserMessages
                .Where(m => m.UserId == uid.Value)
                .OrderByDescending(m => m.CreatedAt)
                .ToList();

            // Kullanıcının ilgilendiği uçuşlar (yan panel)
            var now = DateTime.Now;
            ViewBag.Watched = db.FlightViews
                .Where(v => v.UserId == uid.Value && v.Flight.IsActive && v.Flight.DepartureTime >= now)
                .OrderByDescending(v => v.ViewCount)
                .ThenByDescending(v => v.LastViewedAt)
                .Take(8)
                .ToList();

            ViewBag.MinViews = PriceWatchService.MinViewsForAlert;
            return View(messages);
        }

        // --------------------------
        // READ - POST (tek mesajı okundu yap)
        // --------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Read(int id)
        {
            var uid = CurrentUserId;
            if (uid == null) return Json(new { success = false });

            var m = db.UserMessages.FirstOrDefault(x => x.UserMessageId == id && x.UserId == uid.Value);
            if (m != null && !m.IsRead)
            {
                m.IsRead = true;
                db.SaveChanges();
            }
            return Json(new { success = true, unread = UnreadFor(uid.Value) });
        }

        // --------------------------
        // READ ALL - POST
        // --------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ReadAll()
        {
            var uid = CurrentUserId;
            if (uid == null) return Json(new { success = false });

            var list = db.UserMessages.Where(x => x.UserId == uid.Value && !x.IsRead).ToList();
            list.ForEach(x => x.IsRead = true);
            db.SaveChanges();
            return Json(new { success = true, unread = 0 });
        }

        // --------------------------
        // DELETE - POST
        // --------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Delete(int id)
        {
            var uid = CurrentUserId;
            if (uid == null) return Json(new { success = false });

            var m = db.UserMessages.FirstOrDefault(x => x.UserMessageId == id && x.UserId == uid.Value);
            if (m != null)
            {
                db.UserMessages.Remove(m);
                db.SaveChanges();
            }
            return Json(new { success = true, unread = UnreadFor(uid.Value) });
        }

        // --------------------------
        // UNREAD COUNT - GET (header rozeti + sohbet kutusu için)
        // --------------------------
        [HttpGet]
        public JsonResult UnreadCount()
        {
            var uid = CurrentUserId;
            if (uid == null) return Json(new { loggedIn = false, count = 0 }, JsonRequestBehavior.AllowGet);

            try { PriceWatchService.SyncUser(db, uid.Value); } catch { /* sessiz geç */ }

            var latest = db.UserMessages
                .Where(m => m.UserId == uid.Value && !m.IsRead && (m.Type == "PriceDrop" || m.Type == "WeatherAlert"))
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { id = m.UserMessageId, type = m.Type, titleTr = m.TitleTr, titleEn = m.TitleEn, bodyTr = m.BodyTr, bodyEn = m.BodyEn })
                .FirstOrDefault();

            return Json(new { loggedIn = true, count = UnreadFor(uid.Value), latest = latest }, JsonRequestBehavior.AllowGet);
        }

        private int UnreadFor(int userId)
        {
            return db.UserMessages.Count(m => m.UserId == userId && !m.IsRead);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
