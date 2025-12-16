using System;
using System.Linq;
using System.Web.Mvc;
using Trabzon_Airlines.Data;
using Trabzon_Airlines.Models;

namespace Trabzon_Airlines.Controllers
{
    public class CheckInController : Controller
    {
        private TAContext db = new TAContext();

        // GET: CheckIn
        public ActionResult Index()
        {
            return View();
        }

        // POST: CheckIn/SearchBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SearchBooking(string pnrCode)
        {
            if (string.IsNullOrWhiteSpace(pnrCode))
            {
                TempData["Error"] = "Please enter a valid PNR code.";
                return RedirectToAction("Index");
            }

            var booking = db.Bookings
                           .Include("Flight")
                           .Include("Seat")
                           .Include("BookingMeals.Meal")
                           .FirstOrDefault(b => b.PNR == pnrCode.ToUpper().Trim());

            if (booking == null)
            {
                TempData["Error"] = "Booking not found. Please check your PNR code.";
                return RedirectToAction("Index");
            }

            return View("BookingDetails", booking);
        }

        // POST: CheckIn/DoCheckIn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DoCheckIn(int bookingId)
        {
            var booking = db.Bookings.Find(bookingId);

            if (booking == null)
                return HttpNotFound();

            if (booking.CheckedIn)
            {
                TempData["Info"] = "You have already checked in for this flight.";
                return RedirectToAction("BoardingPass", new { bookingId = bookingId });
            }

            // Perform check-in
            booking.CheckedIn = true;
            booking.CheckInTime = DateTime.Now;
            db.SaveChanges();

            TempData["Success"] = "Check-in successful!";
            return RedirectToAction("BoardingPass", new { bookingId = bookingId });
        }

        // GET: CheckIn/BoardingPass
        public ActionResult BoardingPass(int bookingId)
        {
            var booking = db.Bookings
                           .Include("Flight")
                           .Include("Seat")
                           .FirstOrDefault(b => b.BookingId == bookingId);

            if (booking == null)
                return HttpNotFound();

            if (!booking.CheckedIn)
            {
                TempData["Error"] = "Please complete check-in first.";
                return RedirectToAction("Index");
            }

            return View(booking);
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

