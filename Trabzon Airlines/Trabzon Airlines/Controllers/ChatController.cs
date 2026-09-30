using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using Trabzon_Airlines.Data;
using Trabzon_Airlines.Models;
using Trabzon_Airlines.Services;

namespace Trabzon_Airlines.Controllers
{
    // Sağ alttaki yapay zeka sohbet kutusunun backend'i
    // Backend for the AI chat box (bottom-right)
    public class ChatController : Controller
    {
        private TAContext db = new TAContext();

        private const int MaxMessageLength = 1000;   // Tek mesaj üst sınırı
        private const int MaxHistoryTurns = 12;      // AI'ya gönderilecek son mesaj sayısı
        private const int RateLimitCount = 20;       // Pencere başına mesaj
        private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(5);

        // --------------------------
        // SEND - POST  (/Chat/Send)
        // --------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> Send(string message, string history, string lang)
        {
            bool en = lang == "en";

            if (string.IsNullOrWhiteSpace(message))
                return Fail(en ? "Please write a message." : "Lütfen bir mesaj yazın.");

            message = message.Trim();
            if (message.Length > MaxMessageLength)
                message = message.Substring(0, MaxMessageLength);

            if (!AiChatService.IsConfigured)
                return Fail(en ? "The assistant is not configured yet (missing API key)."
                               : "Asistan henüz yapılandırılmadı (API anahtarı eksik).");

            if (IsRateLimited())
                return Fail(en ? "Too many messages. Please wait a few minutes."
                               : "Çok fazla mesaj gönderdiniz. Lütfen birkaç dakika bekleyin.");

            // İstemciden gelen geçmişi güvenli şekilde oku
            var turns = new List<ChatTurn>();
            try
            {
                var parsed = JsonConvert.DeserializeObject<List<ChatTurn>>(history ?? "[]") ?? new List<ChatTurn>();
                turns = parsed
                    .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Content) && (t.Role == "user" || t.Role == "assistant"))
                    .Select(t => new ChatTurn
                    {
                        Role = t.Role,
                        Content = t.Content.Length > 2000 ? t.Content.Substring(0, 2000) : t.Content
                    })
                    .ToList();
            }
            catch { /* bozuk geçmiş -> yok say */ }

            turns = turns.Skip(Math.Max(0, turns.Count - MaxHistoryTurns)).ToList();

            // Geçmiş "user" ile başlamalı (Gemini/Claude kuralı)
            while (turns.Count > 0 && turns[0].Role != "user") turns.RemoveAt(0);

            turns.Add(new ChatTurn { Role = "user", Content = message });

            try
            {
                var reply = await AiChatService.GetReplyAsync(BuildSystemPrompt(en), turns);
                if (string.IsNullOrWhiteSpace(reply))
                    return Fail(en ? "I couldn't generate an answer, please try again."
                                   : "Şu an yanıt oluşturamadım, lütfen tekrar deneyin.");

                return Json(new { success = true, reply = reply.Trim() });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Chat] " + ex);
                return Fail(en ? "The assistant is unavailable right now. Please try again later."
                               : "Asistana şu an ulaşılamıyor. Lütfen daha sonra tekrar deneyin.");
            }
        }

        // --------------------------
        // Sistem talimatı: marka + site bilgisi + canlı uçuş verisi
        // --------------------------
        private string BuildSystemPrompt(bool en)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Sen Trabzon Airlines web sitesinin yardımcı asistanısın. Adın 'Trabzon Asistan'.");
            sb.AppendLine("Görevin: kullanıcılara uçuş arama, rezervasyon, koltuk ve yemek seçimi, online check-in, uçuş durumu, bagaj ve genel seyahat konularında kısa, net ve samimi şekilde yardım etmek.");
            sb.AppendLine(en ? "Reply in English unless the user writes in another language."
                             : "Kullanıcı başka bir dilde yazmadıkça Türkçe yanıt ver.");
            sb.AppendLine("Kurallar:");
            sb.AppendLine("- Yanıtların kısa olsun (genelde 2-6 cümle). Gerekirse madde işareti (- ) kullan, **kalın** yazıyı az kullan.");
            sb.AppendLine("- Sadece aşağıda verilen uçuş bilgilerini gerçek kabul et. Listede olmayan uçuş, fiyat veya saat UYDURMA.");
            sb.AppendLine("- Ödeme alamazsın, rezervasyon oluşturamaz/iptal edemezsin; kullanıcıyı ilgili sayfaya yönlendir.");
            sb.AppendLine("- Kart bilgisi, şifre gibi hassas bilgileri asla isteme.");
            sb.AppendLine("- Konu havayolu/seyahat dışındaysa kibarca konuya geri dön.");
            sb.AppendLine("- Çözemediğin durumlarda Destek sayfasını (/Home/Support) öner.");
            sb.AppendLine();
            sb.AppendLine("Site sayfaları (link verirken bu yolları kullan):");
            sb.AppendLine("- Uçuş arama / Ana sayfa: /");
            sb.AppendLine("- Online Check-in (PNR + soyadı ile): /CheckIn");
            sb.AppendLine("- Uçuş durumu: /Flights/Status");
            sb.AppendLine("- Rezervasyonlarım (giriş gerekir): /Account/Dashboard");
            sb.AppendLine("- Destek talebi: /Home/Support");
            sb.AppendLine("- İletişim: /Home/Contact");
            sb.AppendLine("- Trip AI gezi planlayıcı: /Home/TripAI");
            sb.AppendLine("- Kabin sınıfları: Ekonomi ve Business. Yolcu tipleri: Yetişkin (12+), Çocuk (2-11), Bebek (0-23 ay).");
            sb.AppendLine();
            sb.AppendLine("Bugünün tarihi: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture));
            sb.AppendLine("Kullanıcı giriş yapmış mı: " + (Session["UserId"] != null ? "Evet" : "Hayır"));
            sb.AppendLine();

            // Giriş yapmış kullanıcının profili, rezervasyonları ve ilgi alanları
            int uid;
            if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out uid))
            {
                try { AppendUserContext(sb, uid); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Chat] User context error: " + ex.Message); }
            }
            else
            {
                sb.AppendLine("Kullanıcı misafir. Kişisel rezervasyon sorularında giriş yapmasını (/Home/Login) veya PNR ile /CheckIn sayfasını kullanmasını öner.");
                sb.AppendLine();
            }

            // Yaklaşan aktif uçuşlar
            try
            {
                var now = DateTime.Now;
                var flights = db.Flights
                    .Where(f => f.IsActive && f.DepartureTime >= now)
                    .OrderBy(f => f.DepartureTime)
                    .Take(40)
                    .ToList();

                var ids = flights.Select(f => f.FlightId).ToList();
                var weather = db.FlightWeathers.Where(w => ids.Contains(w.FlightId)).ToList()
                    .ToDictionary(w => w.FlightId);

                if (flights.Any())
                {
                    sb.AppendLine("Yaklaşan aktif uçuşlar (Sefer | Nereden-Nereye | Kalkış - Varış | Ekonomi / Business fiyat | Hava | Rötar riski):");
                    foreach (var f in flights)
                    {
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "- {0} | {1}-{2} | {3:dd.MM.yyyy HH:mm} - {4:HH:mm} | {5:0.##} TL / {6:0.##} TL | {7} | {8}\n",
                            f.FlightNumber, f.FromCode, f.ToCode, f.DepartureTime, f.ArrivalTime,
                            f.EconomyPrice, f.BusinessPrice,
                            string.IsNullOrEmpty(f.WeatherCondition) ? "-" : f.WeatherCondition,
                            string.IsNullOrEmpty(f.DelayRisk) ? "-" : f.DelayRisk);

                        FlightWeather fw;
                        if (weather.TryGetValue(f.FlightId, out fw) && fw.RiskLevel >= 1)
                            sb.AppendLine("    ↳ Hava tahmini: " + fw.ReasonsTr + " (güncelleme " + fw.UpdatedAt.ToString("dd.MM HH:mm") + ")");
                    }
                    sb.AppendLine("Hava durumu ve rötar riski internetten alınan saatlik tahmine göre otomatik hesaplanır (Open-Meteo). Bunu 'tahmin' olarak sun; kesin rötar/iptal kararını havayolunun vereceğini belirt.");
                }
                else
                {
                    sb.AppendLine("Şu an sistemde yaklaşan aktif uçuş bulunmuyor.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Chat] Flight context error: " + ex.Message);
            }

            return sb.ToString();
        }

        // --------------------------
        // Kullanıcıya özel bağlam: profil + rezervasyonlar + baktığı uçuşlar + mesajlar
        // (Şifre ve TC Kimlik No bilinçli olarak EKLENMEZ)
        // --------------------------
        private void AppendUserContext(StringBuilder sb, int userId)
        {
            var user = db.Users.Find(userId);
            if (user == null) return;

            var now = DateTime.Now;
            var tr = CultureInfo.GetCultureInfo("tr-TR");

            // Güvenlik: kullanıcı verisini PriceWatch ile güncelle (yeni düşüş varsa mesaj oluşsun)
            try { PriceWatchService.SyncUser(db, userId); } catch { }

            sb.AppendLine("=== GİRİŞ YAPMIŞ KULLANICI (bu bilgiler doğrulanmış, kullanıcıya TEKRAR SORMA) ===");
            sb.AppendLine("Ad Soyad: " + user.FirstName + " " + user.LastName);
            sb.AppendLine("E-posta: " + user.Email);
            if (!string.IsNullOrEmpty(user.Phone)) sb.AppendLine("Telefon: " + user.Phone);
            if (!string.IsNullOrEmpty(user.Gender)) sb.AppendLine("Cinsiyet: " + user.Gender);
            if (user.BirthDate > DateTime.MinValue)
            {
                int age = now.Year - user.BirthDate.Year;
                if (user.BirthDate.Date > now.AddYears(-age)) age--;
                sb.AppendLine("Yaş: " + age);
            }
            sb.AppendLine("Kullanıcıya adıyla (" + user.FirstName + ") hitap et. Adını, e-postasını, telefonunu, PNR kodlarını veya rezervasyon bilgilerini ASLA tekrar sorma; aşağıdakileri kullan.");
            sb.AppendLine("Birden fazla rezervasyon varsa ve soru belirsizse, en yakın tarihli yaklaşan uçuşu varsay ve bunu belirt.");
            sb.AppendLine();

            // Rezervasyonlar (Dashboard ile aynı eşleştirme: yolcu adı + soyadı)
            var bookings = db.Bookings
                .Where(b => b.PassengerName == user.FirstName && b.PassengerSurname == user.LastName)
                .OrderByDescending(b => b.Flight.DepartureTime)
                .Take(15)
                .Select(b => new
                {
                    b.FlightId, b.PNR, b.Price, b.CabinClass, b.CheckedIn, b.CheckInTime,
                    SeatNumber = b.Seat.SeatNumber,
                    b.Flight.FlightNumber, b.Flight.FromCode, b.Flight.ToCode,
                    b.Flight.DepartureTime, b.Flight.ArrivalTime, b.Flight.IsActive,
                    b.Flight.DelayRisk, b.Flight.WeatherCondition,
                    Meals = b.BookingMeals.Select(m => new { m.Meal.MealName, m.Quantity })
                })
                .ToList();

            var bookingFlightIds = bookings.Select(b => b.FlightId).Distinct().ToList();
            var bookingWeather = db.FlightWeathers.Where(w => bookingFlightIds.Contains(w.FlightId)).ToList()
                .ToDictionary(w => w.FlightId);

            if (bookings.Any())
            {
                sb.AppendLine("Rezervasyonları:");
                foreach (var b in bookings)
                {
                    string status = b.DepartureTime >= now ? "YAKLAŞAN" : "GEÇMİŞ";
                    string checkin = b.CheckedIn ? "check-in yapıldı" : (b.DepartureTime >= now ? "check-in yapılmadı" : "-");
                    var meals = b.Meals.Select(m => m.MealName + " x" + m.Quantity).ToList();
                    sb.AppendLine(string.Format(tr,
                        "- [{0}] PNR {1} | {2} {3}-{4} | {5:dd.MM.yyyy HH:mm} - {6:HH:mm} | Koltuk {7} ({8}) | {9:N0} ₺ | {10} | Yemek: {11} | Rötar riski: {12} | Hava: {13}{14}",
                        status, b.PNR ?? "-", b.FlightNumber, b.FromCode, b.ToCode, b.DepartureTime, b.ArrivalTime,
                        b.SeatNumber ?? "-", b.CabinClass ?? "-", b.Price, checkin,
                        meals.Any() ? string.Join(", ", meals) : "yok",
                        b.DelayRisk ?? "-", b.WeatherCondition ?? "-",
                        b.IsActive ? "" : " | UÇUŞ PASİF/İPTAL"));

                    FlightWeather bw;
                    if (b.DepartureTime >= now && bookingWeather.TryGetValue(b.FlightId, out bw))
                    {
                        sb.AppendLine(string.Format(tr,
                            "    ↳ Hava tahmini ({0:dd.MM HH:mm} itibarıyla): {1} {2}, rüzgâr hamlesi {3:0} km/s, görüş {4:0} m → {5} {6}, rüzgâr hamlesi {7:0} km/s, görüş {8:0} m | Rötar riski: {9} | {10}",
                            bw.UpdatedAt, b.FromCode, bw.FromLabel ?? "-", bw.FromGustKmh ?? 0, bw.FromVisibilityM ?? 0,
                            b.ToCode, bw.ToLabel ?? "-", bw.ToGustKmh ?? 0, bw.ToVisibilityM ?? 0,
                            WeatherService.LevelTr(bw.RiskLevel), bw.ReasonsTr));
                    }
                }
                sb.AppendLine("Toplam rezervasyon: " + bookings.Count + ". Rezervasyon iptali /Account/Dashboard sayfasından yapılır.");
            }
            else
            {
                sb.AppendLine("Kullanıcının henüz rezervasyonu yok.");
            }
            sb.AppendLine();

            // En çok aradığı rotalar
            var routes = db.RouteSearches
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.SearchCount)
                .Take(8)
                .ToList();
            if (routes.Any())
            {
                sb.AppendLine("En çok aradığı rotalar:");
                foreach (var r in routes)
                    sb.AppendLine(string.Format(tr, "- {0}-{1}: {2} kez (son: {3:dd.MM.yyyy HH:mm})", r.FromCode, r.ToCode, r.SearchCount, r.LastSearchedAt));
                sb.AppendLine();
            }

            // İlgilendiği (baktığı) uçuşlar ve güncel fiyat
            var views = db.FlightViews
                .Where(v => v.UserId == userId && v.Flight.DepartureTime >= now)
                .OrderByDescending(v => v.ViewCount)
                .Take(10)
                .Select(v => new
                {
                    v.ViewCount, v.LastSeenEconomyPrice,
                    v.Flight.FlightNumber, v.Flight.FromCode, v.Flight.ToCode,
                    v.Flight.DepartureTime, v.Flight.EconomyPrice, v.Flight.BusinessPrice
                })
                .ToList();
            if (views.Any())
            {
                sb.AppendLine("Kullanıcının baktığı yaklaşan uçuşlar (fiyat takibi " + PriceWatchService.MinViewsForAlert + "+ bakışta aktif):");
                foreach (var v in views)
                    sb.AppendLine(string.Format(tr, "- {0} {1}-{2} {3:dd.MM.yyyy HH:mm} | {4} kez baktı | Güncel: Ekonomi {5:N0} ₺ / Business {6:N0} ₺",
                        v.FlightNumber, v.FromCode, v.ToCode, v.DepartureTime, v.ViewCount, v.EconomyPrice, v.BusinessPrice));
                sb.AppendLine("Kullanıcı birden çok kez baktığı bir uçuşla ilgileniyorsa, fiyat düşerse /Messages sayfasına bildirim geleceğini söyleyebilirsin.");
                sb.AppendLine();
            }

            // Okunmamış mesajlar (fiyat düştü vb.)
            var unread = db.UserMessages
                .Where(m => m.UserId == userId && !m.IsRead)
                .OrderByDescending(m => m.CreatedAt)
                .Take(5)
                .ToList();
            if (unread.Any())
            {
                sb.AppendLine("Okunmamış mesajları (" + unread.Count + "):");
                foreach (var m in unread) sb.AppendLine("- " + m.TitleTr + ": " + m.BodyTr);
                sb.AppendLine("Uygunsa kullanıcıya bu fiyat düşüşlerini hatırlat ve /Messages sayfasını öner.");
                sb.AppendLine();
            }
        }

        // Basit, oturum bazlı hız sınırı
        private bool IsRateLimited()
        {
            var now = DateTime.UtcNow;
            var stamps = (Session["ChatStamps"] as List<DateTime>) ?? new List<DateTime>();
            stamps = stamps.Where(t => now - t < RateWindow).ToList();
            if (stamps.Count >= RateLimitCount)
            {
                Session["ChatStamps"] = stamps;
                return true;
            }
            stamps.Add(now);
            Session["ChatStamps"] = stamps;
            return false;
        }

        private JsonResult Fail(string msg)
        {
            return Json(new { success = false, message = msg });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
