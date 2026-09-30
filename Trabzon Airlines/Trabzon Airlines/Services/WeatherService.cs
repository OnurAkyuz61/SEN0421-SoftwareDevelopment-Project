using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Trabzon_Airlines.Data;
using Trabzon_Airlines.Models;

namespace Trabzon_Airlines.Services
{
    // Havalimanı hava tahmini (Open-Meteo, ücretsiz / API anahtarı gerekmez)
    // + rötar/iptal riski hesaplama + yolculara uyarı mesajı
    public static class WeatherService
    {
        // Uçuştan kaç saat önceye kadar yolcuya uyarı gönderilir
        public const int AlertWindowHours = 72;
        // Kaç gün ilerisine kadar uçuşların hava durumu güncellenir (Open-Meteo max 16)
        public const int UpdateWindowDays = 7;

        private static readonly HttpClient Http;
        private static readonly ConcurrentDictionary<string, Tuple<DateTime, JObject>> Cache =
            new ConcurrentDictionary<string, Tuple<DateTime, JObject>>();
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(60);
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static WeatherService()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        }

        // ---------------------------------------------------------
        // HAVALİMANI KOORDİNATLARI (airports.js ile aynı kodlar)
        // ---------------------------------------------------------
        private class Airport
        {
            public string City; public double Lat; public double Lon;
            public Airport(string c, double la, double lo) { City = c; Lat = la; Lon = lo; }
        }

        private static readonly Dictionary<string, Airport> Airports = new Dictionary<string, Airport>
        {
            { "IST", new Airport("İstanbul", 41.262, 28.742) },
            { "SAW", new Airport("İstanbul (Sabiha Gökçen)", 40.899, 29.309) },
            { "TZX", new Airport("Trabzon", 40.995, 39.790) },
            { "ESB", new Airport("Ankara", 40.128, 32.995) },
            { "ADB", new Airport("İzmir", 38.292, 27.157) },
            { "IZM", new Airport("İzmir", 38.292, 27.157) },
            { "AYT", new Airport("Antalya", 36.899, 30.800) },
            { "ADA", new Airport("Adana", 36.982, 35.280) },
            { "COV", new Airport("Çukurova", 36.890, 35.070) },
            { "ADF", new Airport("Adıyaman", 37.731, 38.469) },
            { "AJI", new Airport("Ağrı", 39.654, 43.026) },
            { "AOE", new Airport("Eskişehir", 39.810, 30.519) },
            { "ASR", new Airport("Kayseri", 38.770, 35.495) },
            { "BAL", new Airport("Batman", 37.929, 41.117) },
            { "BGG", new Airport("Bingöl", 38.861, 40.593) },
            { "BJV", new Airport("Bodrum", 37.251, 27.664) },
            { "BTZ", new Airport("Bursa", 40.255, 29.562) },
            { "YEI", new Airport("Bursa", 40.255, 29.562) },
            { "BZI", new Airport("Balıkesir", 39.619, 27.926) },
            { "EDO", new Airport("Edremit", 39.555, 27.014) },
            { "CII", new Airport("Aydın", 37.815, 27.887) },
            { "CKZ", new Airport("Çanakkale", 40.138, 26.427) },
            { "DIY", new Airport("Diyarbakır", 37.894, 40.201) },
            { "DLM", new Airport("Dalaman", 36.713, 28.792) },
            { "DNZ", new Airport("Denizli", 37.786, 29.701) },
            { "ERC", new Airport("Erzincan", 39.710, 39.527) },
            { "ERZ", new Airport("Erzurum", 39.956, 41.170) },
            { "EZS", new Airport("Elazığ", 38.607, 39.291) },
            { "GKD", new Airport("Gökçeada", 40.205, 25.883) },
            { "GNY", new Airport("Şanlıurfa", 37.446, 38.896) },
            { "GZP", new Airport("Gazipaşa", 36.299, 32.301) },
            { "GZT", new Airport("Gaziantep", 36.947, 37.479) },
            { "HTY", new Airport("Hatay", 36.363, 36.282) },
            { "IGD", new Airport("Iğdır", 39.977, 43.866) },
            { "ISE", new Airport("Isparta", 37.855, 30.368) },
            { "KCM", new Airport("Kahramanmaraş", 37.539, 36.954) },
            { "KCO", new Airport("Kocaeli", 40.735, 30.083) },
            { "KFS", new Airport("Kastamonu", 41.314, 33.796) },
            { "KSY", new Airport("Kars", 40.562, 43.115) },
            { "KYA", new Airport("Konya", 37.979, 32.562) },
            { "KZR", new Airport("Kütahya", 39.113, 30.128) },
            { "MLX", new Airport("Malatya", 38.435, 38.091) },
            { "MQM", new Airport("Mardin", 37.223, 40.632) },
            { "MSR", new Airport("Muş", 38.748, 41.661) },
            { "MZH", new Airport("Amasya", 40.829, 35.522) },
            { "NAV", new Airport("Nevşehir", 38.772, 34.535) },
            { "NKT", new Airport("Şırnak", 37.365, 42.058) },
            { "NOP", new Airport("Sinop", 42.016, 35.066) },
            { "OGU", new Airport("Ordu-Giresun", 40.966, 38.080) },
            { "ONQ", new Airport("Zonguldak", 41.506, 32.088) },
            { "RZV", new Airport("Rize-Artvin", 41.170, 40.850) },
            { "SXZ", new Airport("Siirt", 37.979, 41.840) },
            { "SZF", new Airport("Samsun", 41.254, 36.567) },
            { "TEQ", new Airport("Tekirdağ", 41.138, 27.919) },
            { "TJK", new Airport("Tokat", 40.307, 36.367) },
            { "USQ", new Airport("Uşak", 38.681, 29.472) },
            { "VAN", new Airport("Van", 38.468, 43.332) },
            { "VAS", new Airport("Sivas", 39.814, 36.903) },
            { "YKO", new Airport("Hakkari", 37.549, 44.234) },
        };

        public static string CityOf(string code)
        {
            Airport a;
            return code != null && Airports.TryGetValue(code.ToUpperInvariant(), out a) ? a.City : code;
        }

        // ---------------------------------------------------------
        // TAHMİN NOKTASI
        // ---------------------------------------------------------
        public class WeatherPoint
        {
            public int Code;
            public double? TempC, WindKmh, GustKmh, VisibilityM, PrecipMm, SnowCm;
        }

        public class Assessment
        {
            public int Level;                         // 0 Düşük, 1 Orta, 2 Yüksek
            public string Label;                      // "Karlı"
            public List<string> ReasonsTr = new List<string>();
            public List<string> ReasonsEn = new List<string>();
        }

        public static string LevelTr(int l) { return l >= 2 ? "Yüksek" : l == 1 ? "Orta" : "Düşük"; }
        public static string LevelEn(int l) { return l >= 2 ? "High" : l == 1 ? "Medium" : "Low"; }

        // Belirli havalimanı ve saat için tahmini getir (null = alınamadı)
        public static WeatherPoint GetForecast(string code, DateTime localTime)
        {
            Airport ap;
            if (code == null || !Airports.TryGetValue(code.ToUpperInvariant(), out ap)) return null;

            var date = localTime.Date;
            if (date < DateTime.Today || date > DateTime.Today.AddDays(15)) return null;

            string key = code.ToUpperInvariant() + "|" + date.ToString("yyyy-MM-dd", Inv);
            Tuple<DateTime, JObject> cached;
            JObject hourly;

            if (Cache.TryGetValue(key, out cached) && DateTime.Now - cached.Item1 < CacheTtl)
            {
                hourly = cached.Item2;
            }
            else
            {
                string url = string.Format(Inv,
                    "https://api.open-meteo.com/v1/forecast?latitude={0:0.###}&longitude={1:0.###}" +
                    "&hourly=temperature_2m,weather_code,wind_speed_10m,wind_gusts_10m,visibility,precipitation,snowfall" +
                    "&timezone=Europe%2FIstanbul&start_date={2:yyyy-MM-dd}&end_date={2:yyyy-MM-dd}",
                    ap.Lat, ap.Lon, date);

                // Task.Run: ASP.NET istek thread'inde deadlock olmaması için
                string json = Task.Run(async () =>
                {
                    using (var res = await Http.GetAsync(url).ConfigureAwait(false))
                    {
                        var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!res.IsSuccessStatusCode)
                            throw new HttpRequestException("Open-Meteo " + (int)res.StatusCode + ": " + body);
                        return body;
                    }
                }).GetAwaiter().GetResult();

                hourly = JObject.Parse(json)["hourly"] as JObject;
                if (hourly == null) return null;
                Cache[key] = Tuple.Create(DateTime.Now, hourly);
            }

            var times = hourly["time"] as JArray;
            if (times == null || times.Count == 0) return null;

            // En yakın saati bul
            int best = 0; double bestDiff = double.MaxValue;
            for (int i = 0; i < times.Count; i++)
            {
                DateTime t;
                if (DateTime.TryParse((string)times[i], Inv, DateTimeStyles.None, out t))
                {
                    double d = Math.Abs((t - localTime).TotalMinutes);
                    if (d < bestDiff) { bestDiff = d; best = i; }
                }
            }

            return new WeatherPoint
            {
                Code = (int)(Num(hourly, "weather_code", best) ?? 0),
                TempC = Num(hourly, "temperature_2m", best),
                WindKmh = Num(hourly, "wind_speed_10m", best),
                GustKmh = Num(hourly, "wind_gusts_10m", best),
                VisibilityM = Num(hourly, "visibility", best),
                PrecipMm = Num(hourly, "precipitation", best),
                SnowCm = Num(hourly, "snowfall", best)
            };
        }

        private static double? Num(JObject h, string field, int i)
        {
            var arr = h[field] as JArray;
            if (arr == null || i >= arr.Count || arr[i].Type == JTokenType.Null) return null;
            return (double)arr[i];
        }

        // ---------------------------------------------------------
        // RİSK HESABI
        // ---------------------------------------------------------
        public static Assessment Assess(WeatherPoint p, string city, int hour)
        {
            var a = new Assessment { Label = Label(p, hour) };
            if (p == null) return a;

            Action<int, string, string> add = (lvl, tr, en) =>
            {
                a.Level = Math.Max(a.Level, lvl);
                a.ReasonsTr.Add(city + ": " + tr);
                a.ReasonsEn.Add(city + ": " + en);
            };

            double gust = p.GustKmh ?? 0, vis = p.VisibilityM ?? 99999, snow = p.SnowCm ?? 0, rain = p.PrecipMm ?? 0;

            // --- YÜKSEK ---
            if (p.Code >= 95) add(2, "gök gürültülü fırtına", "thunderstorm");
            if (gust >= 60) add(2, string.Format(Inv, "{0:0} km/s rüzgâr hamlesi", gust), string.Format(Inv, "{0:0} km/h wind gusts", gust));
            else if (gust >= 40) add(1, string.Format(Inv, "{0:0} km/s rüzgâr hamlesi", gust), string.Format(Inv, "{0:0} km/h wind gusts", gust));

            if (vis < 800) add(2, string.Format(Inv, "yoğun sis (görüş {0:0} m)", vis), string.Format(Inv, "dense fog (visibility {0:0} m)", vis));
            else if (vis < 3000) add(1, string.Format(Inv, "düşük görüş ({0:0} m)", vis), string.Format(Inv, "low visibility ({0:0} m)", vis));
            else if (p.Code == 45 || p.Code == 48) add(1, "sis", "fog");

            if (snow >= 2 || p.Code == 75 || p.Code == 86) add(2, string.Format(Inv, "yoğun kar ({0:0.#} cm/saat)", snow), string.Format(Inv, "heavy snow ({0:0.#} cm/h)", snow));
            else if (snow > 0.2 || (p.Code >= 71 && p.Code <= 77) || p.Code == 85) add(1, "kar yağışı", "snowfall");

            if (p.Code == 66 || p.Code == 67) add(2, "donan yağmur (buzlanma)", "freezing rain (icing)");
            else if (p.Code == 56 || p.Code == 57) add(1, "donan çisenti", "freezing drizzle");

            if (rain >= 8 || p.Code == 82) add(1, string.Format(Inv, "şiddetli yağış ({0:0.#} mm/saat)", rain), string.Format(Inv, "heavy rain ({0:0.#} mm/h)", rain));

            return a;
        }

        // WMO hava kodu -> sitedeki etiketler (admin dropdown ile uyumlu)
        private static string Label(WeatherPoint p, int hour)
        {
            if (p == null) return null;
            int c = p.Code;
            if (c >= 95) return "Fırtınalı";
            if ((c >= 71 && c <= 77) || c == 85 || c == 86) return "Karlı";
            if ((c >= 51 && c <= 67) || (c >= 80 && c <= 82)) return "Yağmurlu";
            if (c == 45 || c == 48) return "Sisli";
            if ((p.GustKmh ?? 0) >= 50) return "Rüzgarlı";
            if (c == 3) return "Bulutlu";
            if (c == 1 || c == 2) return "Parçalı Bulutlu";
            return (hour >= 6 && hour < 20) ? "Güneşli" : "Açık";
        }

        // ---------------------------------------------------------
        // TÜM UÇUŞLARI GÜNCELLE (zamanlayıcı / admin butonu)
        // ---------------------------------------------------------
        public class RunResult { public int Updated; public int Alerts; public int Failed; }

        public static RunResult RunAll(TAContext db)
        {
            var result = new RunResult();
            var now = DateTime.Now;
            var until = now.AddDays(UpdateWindowDays);

            var flights = db.Flights
                .Where(f => f.IsActive && f.DepartureTime > now && f.DepartureTime <= until)
                .ToList();

            foreach (var f in flights)
            {
                try
                {
                    result.Alerts += UpdateFlight(db, f);
                    result.Updated++;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    System.Diagnostics.Debug.WriteLine("[Weather] " + f.FlightNumber + ": " + ex.Message);
                }
            }
            db.SaveChanges();
            return result;
        }

        // Tek uçuşu güncelle. Gönderilen uyarı mesajı sayısını döner.
        public static int UpdateFlight(TAContext db, Flight f)
        {
            var from = GetForecast(f.FromCode, f.DepartureTime);
            var to = GetForecast(f.ToCode, f.ArrivalTime);
            if (from == null && to == null) return 0;

            var aFrom = Assess(from, CityOf(f.FromCode), f.DepartureTime.Hour);
            var aTo = Assess(to, CityOf(f.ToCode), f.ArrivalTime.Hour);

            var fw = db.FlightWeathers.FirstOrDefault(x => x.FlightId == f.FlightId);
            if (fw == null)
            {
                fw = new FlightWeather { FlightId = f.FlightId, LastAlertLevel = -1 };
                db.FlightWeathers.Add(fw);
            }

            // Simülasyon aktifse gerçek veriyle ezme (test bitene kadar)
            if (fw.IsSimulated && fw.UpdatedAt > DateTime.Now.AddHours(-6)) return 0;
            fw.IsSimulated = false;

            Fill(fw, from, aFrom, to, aTo);
            return ApplyAndAlert(db, f, fw);
        }

        // ---------------------------------------------------------
        // TEST: Uçuş için kötü hava simüle et (admin)
        // ---------------------------------------------------------
        public static int Simulate(TAContext db, Flight f)
        {
            var storm = new WeatherPoint { Code = 95, TempC = 4, WindKmh = 45, GustKmh = 72, VisibilityM = 600, PrecipMm = 6, SnowCm = 0 };
            var calm = new WeatherPoint { Code = 1, TempC = 16, WindKmh = 10, GustKmh = 18, VisibilityM = 20000, PrecipMm = 0, SnowCm = 0 };

            var aFrom = Assess(calm, CityOf(f.FromCode), f.DepartureTime.Hour);
            var aTo = Assess(storm, CityOf(f.ToCode), f.ArrivalTime.Hour);

            var fw = db.FlightWeathers.FirstOrDefault(x => x.FlightId == f.FlightId);
            if (fw == null)
            {
                fw = new FlightWeather { FlightId = f.FlightId };
                db.FlightWeathers.Add(fw);
            }
            fw.LastAlertLevel = -1;     // testte her zaman mesaj gitsin
            fw.IsSimulated = true;

            Fill(fw, calm, aFrom, storm, aTo);
            int sent = ApplyAndAlert(db, f, fw, true);
            db.SaveChanges();
            return sent;
        }

        private static void Fill(FlightWeather fw, WeatherPoint from, Assessment aFrom, WeatherPoint to, Assessment aTo)
        {
            if (from != null)
            {
                fw.FromLabel = aFrom.Label; fw.FromTempC = from.TempC; fw.FromWindKmh = from.WindKmh; fw.FromGustKmh = from.GustKmh;
                fw.FromVisibilityM = from.VisibilityM; fw.FromPrecipMm = from.PrecipMm; fw.FromSnowCm = from.SnowCm;
            }
            if (to != null)
            {
                fw.ToLabel = aTo.Label; fw.ToTempC = to.TempC; fw.ToWindKmh = to.WindKmh; fw.ToGustKmh = to.GustKmh;
                fw.ToVisibilityM = to.VisibilityM; fw.ToPrecipMm = to.PrecipMm; fw.ToSnowCm = to.SnowCm;
            }

            fw.RiskLevel = Math.Max(aFrom.Level, aTo.Level);
            var rTr = aFrom.ReasonsTr.Concat(aTo.ReasonsTr).ToList();
            var rEn = aFrom.ReasonsEn.Concat(aTo.ReasonsEn).ToList();
            fw.ReasonsTr = Trim(rTr.Any() ? string.Join(", ", rTr) : "Belirgin bir hava riski yok", 500);
            fw.ReasonsEn = Trim(rEn.Any() ? string.Join(", ", rEn) : "No significant weather risk", 500);
            fw.UpdatedAt = DateTime.Now;
        }

        // Flight tablosunu güncelle, gerekirse yolculara mesaj at
        private static int ApplyAndAlert(TAContext db, Flight f, FlightWeather fw, bool simulated = false)
        {
            // Uçuş Durumu sayfasında görünen alanlar
            string worseLabel = (fw.ToLabel != null && Severity(fw.ToLabel) >= Severity(fw.FromLabel)) ? fw.ToLabel : fw.FromLabel;
            if (!string.IsNullOrEmpty(worseLabel)) f.WeatherCondition = worseLabel;
            f.DelayRisk = LevelTr(fw.RiskLevel);

            // Risk tamamen düştüyse bir sonraki yükselişte tekrar uyarı gönderilebilsin
            if (fw.RiskLevel == 0) fw.LastAlertLevel = -1;

            bool inWindow = f.DepartureTime > DateTime.Now && f.DepartureTime <= DateTime.Now.AddHours(AlertWindowHours);
            if (fw.RiskLevel < 2 || fw.LastAlertLevel >= 2 || (!inWindow && !simulated)) return 0;

            // Bu uçuşta bileti olan kullanıcılar (rezervasyonlar yolcu adı-soyadı ile eşleşir)
            var passengers = db.Bookings
                .Where(b => b.FlightId == f.FlightId)
                .Select(b => new { b.PassengerName, b.PassengerSurname, b.PNR })
                .ToList();

            var users = db.Users.ToList()
                .Select(u => new { User = u, Booking = passengers.FirstOrDefault(p => p.PassengerName == u.FirstName && p.PassengerSurname == u.LastName) })
                .Where(x => x.Booking != null)
                .ToList();

            var tr = CultureInfo.GetCultureInfo("tr-TR");
            var en = CultureInfo.GetCultureInfo("en-US");
            string test = simulated ? "[TEST] " : "";
            string route = f.FromCode + " → " + f.ToCode;

            foreach (var x in users)
            {
                db.UserMessages.Add(new UserMessage
                {
                    UserId = x.User.UserId,
                    Type = "WeatherAlert",
                    FlightId = f.FlightId,
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    TitleTr = Trim(test + "Hava durumu uyarısı: " + route + " (" + f.FlightNumber + ")", 200),
                    TitleEn = Trim(test + "Weather alert: " + route + " (" + f.FlightNumber + ")", 200),
                    BodyTr = Trim(string.Format(tr,
                        "Sayın {0}, {1:dd MMMM yyyy HH:mm} kalkışlı {2} seferiniz (PNR {3}) için {4} bekleniyor. Rötar veya iptal riski YÜKSEK. Havalimanına çıkmadan önce Uçuş Durumu sayfasını kontrol edin. Not: Bu bir hava tahminidir, kesin kararı havayolu verir.",
                        x.User.FirstName, f.DepartureTime, f.FlightNumber, x.Booking.PNR ?? "-", fw.ReasonsTr), 1000),
                    BodyEn = Trim(string.Format(en,
                        "Dear {0}, for your flight {2} departing {1:MMMM dd, yyyy HH:mm} (PNR {3}) the forecast shows {4}. Delay or cancellation risk is HIGH. Please check the Flight Status page before leaving for the airport. Note: this is a forecast; the final decision is made by the airline.",
                        x.User.FirstName, f.DepartureTime, f.FlightNumber, x.Booking.PNR ?? "-", fw.ReasonsEn), 1000)
                });
            }

            fw.LastAlertLevel = 2;
            return users.Count;
        }

        private static int Severity(string label)
        {
            switch (label)
            {
                case "Fırtınalı": return 6;
                case "Karlı": return 5;
                case "Sisli": return 4;
                case "Rüzgarlı": return 3;
                case "Yağmurlu": return 2;
                case "Bulutlu": return 1;
                default: return 0;
            }
        }

        private static string Trim(string s, int max) { return s == null || s.Length <= max ? s : s.Substring(0, max - 1) + "…"; }
    }

    // ---------------------------------------------------------
    // ARKA PLAN ZAMANLAYICI - Global.asax Application_Start'ta başlatılır
    // ---------------------------------------------------------
    public static class WeatherScheduler
    {
        private static Timer _timer;
        private static int _running;
        public static DateTime? LastRun { get; private set; }
        public static string LastResult { get; private set; }

        public static void Start()
        {
            int minutes;
            if (!int.TryParse(ConfigurationManager.AppSettings["Weather:IntervalMinutes"], out minutes) || minutes < 15)
                minutes = 180;

            if (string.Equals(ConfigurationManager.AppSettings["Weather:Enabled"], "false", StringComparison.OrdinalIgnoreCase))
                return;

            // İlk çalışma uygulama açıldıktan 30 sn sonra
            _timer = new Timer(_ => RunNow(), null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(minutes));
        }

        public static WeatherService.RunResult RunNow()
        {
            // Aynı anda iki çalışma olmasın
            if (Interlocked.Exchange(ref _running, 1) == 1) return null;
            try
            {
                using (var db = new TAContext())
                {
                    var r = WeatherService.RunAll(db);
                    LastRun = DateTime.Now;
                    LastResult = r.Updated + " uçuş güncellendi, " + r.Alerts + " uyarı gönderildi, " + r.Failed + " hata";
                    System.Diagnostics.Debug.WriteLine("[Weather] " + LastResult);
                    return r;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Weather] Run error: " + ex);
                return null;
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }
    }
}
