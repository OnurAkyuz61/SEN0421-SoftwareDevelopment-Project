using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml.Linq;

namespace Trabzon_Airlines.Controllers
{
    public class SitemapController : Controller
    {
        public ActionResult Index()
        {
            var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
            var sitemap = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("urlset",
                    new XAttribute("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9"),
                    
                    // Ana Sayfa
                    CreateUrlElement(baseUrl + "/", DateTime.Now, "1.0", "daily"),
                    
                    // Home Controller Sayfaları
                    CreateUrlElement(baseUrl + "/Home/About", DateTime.Now, "0.8", "monthly"),
                    CreateUrlElement(baseUrl + "/Home/Contact", DateTime.Now, "0.8", "monthly"),
                    CreateUrlElement(baseUrl + "/Home/Login", DateTime.Now, "0.7", "monthly"),
                    CreateUrlElement(baseUrl + "/Home/Signup", DateTime.Now, "0.7", "monthly"),
                    CreateUrlElement(baseUrl + "/Home/TripAI", DateTime.Now, "0.8", "weekly"),
                    
                    // Flights Controller Sayfaları
                    CreateUrlElement(baseUrl + "/Flights", DateTime.Now, "0.9", "daily"),
                    CreateUrlElement(baseUrl + "/Flights/Index", DateTime.Now, "0.9", "daily"),
                    CreateUrlElement(baseUrl + "/Flights/Status", DateTime.Now, "0.8", "daily"),
                    
                    // CheckIn Controller Sayfaları
                    CreateUrlElement(baseUrl + "/CheckIn", DateTime.Now, "0.8", "weekly"),
                    CreateUrlElement(baseUrl + "/CheckIn/Index", DateTime.Now, "0.8", "weekly"),
                    
                    // Booking Controller Sayfaları (genel linkler)
                    CreateUrlElement(baseUrl + "/Booking/SelectSeat", DateTime.Now, "0.7", "weekly"),
                    CreateUrlElement(baseUrl + "/Booking/MealSelection", DateTime.Now, "0.7", "weekly"),
                    
                    // Admin Login (public erişilebilir)
                    CreateUrlElement(baseUrl + "/Admin/Login", DateTime.Now, "0.5", "monthly")
                )
            );

            Response.ContentType = "text/xml";
            Response.ContentEncoding = System.Text.Encoding.UTF8;
            return Content(sitemap.ToString(), "text/xml");
        }

        private XElement CreateUrlElement(string loc, DateTime lastmod, string priority, string changefreq)
        {
            return new XElement("url",
                new XElement("loc", loc),
                new XElement("lastmod", lastmod.ToString("yyyy-MM-dd")),
                new XElement("changefreq", changefreq),
                new XElement("priority", priority)
            );
        }
    }
}

