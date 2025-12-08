using System.Web;
using System.Web.Optimization;

namespace Trabzon_Airlines
{
    public class BundleConfig
    {
        // For more information on bundling, visit https://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate*"));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at https://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new Bundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/css/site.css",
                      "~/Content/css/style.css"));

            bundles.Add(new StyleBundle("~/Content/auth").Include(
                      "~/Content/css/auth.css"));

            bundles.Add(new StyleBundle("~/Content/about").Include(
                      "~/Content/css/about.css"));

            bundles.Add(new StyleBundle("~/Content/contact").Include(
                      "~/Content/css/contact.css"));

            // Trabzon Airlines JavaScript bundles
            // ScriptBundle yerine Bundle kullanarak minification'ı devre dışı bırakıyoruz
            bundles.Add(new Bundle("~/bundles/trabzonairlines").Include(
                      "~/js/airports.js",
                      "~/js/language.js",
                      "~/js/sidebar.js",
                      "~/js/flight-search.js",
                      "~/js/forms.js",
                      "~/js/alerts.js",
                      "~/js/multi-city.js",
                      "~/js/trip-type.js",
                      "~/js/main.js"));
            
            // Sayfa özel CSS bundle'ları
            bundles.Add(new StyleBundle("~/Content/seatmap").Include(
                      "~/Content/css/seatmap.css"));
            
            bundles.Add(new StyleBundle("~/Content/trip").Include(
                      "~/Content/css/trip.css"));
            
            // Sayfa özel JS bundle'ları
            bundles.Add(new Bundle("~/bundles/tripai").Include(
                      "~/js/tripai.js"));
            
            bundles.Add(new Bundle("~/bundles/auth").Include(
                      "~/js/auth.js"));
        }
    }
}
