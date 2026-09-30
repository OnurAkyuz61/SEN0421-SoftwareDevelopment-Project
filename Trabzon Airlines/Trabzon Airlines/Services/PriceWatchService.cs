using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Trabzon_Airlines.Data;
using Trabzon_Airlines.Models;

namespace Trabzon_Airlines.Services
{
    // Fiyat takibi servisi
    // 1) Kullanıcının aradığı rotaları ve gördüğü uçuşları kaydeder
    // 2) Takip edilen bir uçuşun fiyatı düşerse kullanıcıya mesaj oluşturur
    public static class PriceWatchService
    {
        // Bir uçuşun "ilgilenilen" sayılması için en az kaç defa bakılmış olmalı
        public const int MinViewsForAlert = 2;

        // ---------------------------------------------------------
        // ARAMA KAYDI - FlightsController.Search içinden çağrılır
        // ---------------------------------------------------------
        public static void TrackSearch(TAContext db, int userId, string fromCode, string toCode, IEnumerable<Flight> shownFlights)
        {
            var now = DateTime.Now;

            // 1) Rota arama sayacı
            if (!string.IsNullOrWhiteSpace(fromCode) && !string.IsNullOrWhiteSpace(toCode))
            {
                fromCode = fromCode.Trim().ToUpperInvariant();
                toCode = toCode.Trim().ToUpperInvariant();

                var route = db.RouteSearches.FirstOrDefault(r => r.UserId == userId && r.FromCode == fromCode && r.ToCode == toCode);
                if (route == null)
                {
                    db.RouteSearches.Add(new RouteSearch
                    {
                        UserId = userId,
                        FromCode = fromCode,
                        ToCode = toCode,
                        SearchCount = 1,
                        FirstSearchedAt = now,
                        LastSearchedAt = now
                    });
                }
                else
                {
                    route.SearchCount++;
                    route.LastSearchedAt = now;
                }
            }

            // 2) Sonuçlarda gösterilen her uçuş için görüntülenme sayacı
            foreach (var f in shownFlights ?? Enumerable.Empty<Flight>())
            {
                var view = db.FlightViews.FirstOrDefault(v => v.UserId == userId && v.FlightId == f.FlightId);
                if (view == null)
                {
                    db.FlightViews.Add(new FlightView
                    {
                        UserId = userId,
                        FlightId = f.FlightId,
                        ViewCount = 1,
                        FirstViewedAt = now,
                        LastViewedAt = now,
                        LastSeenEconomyPrice = f.EconomyPrice,
                        LastSeenBusinessPrice = f.BusinessPrice
                    });
                }
                else
                {
                    // Kullanıcı uçuşa tekrar baktı: sayacı artır, fiyat düşmüş mü kontrol et
                    view.ViewCount++;
                    view.LastViewedAt = now;
                    CheckView(db, view, f);
                    view.LastSeenEconomyPrice = f.EconomyPrice;
                    view.LastSeenBusinessPrice = f.BusinessPrice;
                }
            }

            db.SaveChanges();
        }

        // ---------------------------------------------------------
        // UÇUŞ FİYATI DEĞİŞTİ - Admin uçuşu düzenleyince çağrılır
        // Bu uçuşu takip eden TÜM kullanıcılar kontrol edilir
        // ---------------------------------------------------------
        public static int CheckFlight(TAContext db, int flightId)
        {
            var flight = db.Flights.Find(flightId);
            if (flight == null) return 0;

            var views = db.FlightViews.Where(v => v.FlightId == flightId && v.ViewCount >= MinViewsForAlert).ToList();
            int created = 0;
            foreach (var v in views)
            {
                if (CheckView(db, v, flight)) created++;
            }
            db.SaveChanges();
            return created;
        }

        // ---------------------------------------------------------
        // KULLANICI SENKRONU - Mesajlar sayfası / sohbet açılınca çağrılır
        // (Fiyat veritabanından elle değiştirilmişse de yakalanır)
        // ---------------------------------------------------------
        public static int SyncUser(TAContext db, int userId)
        {
            var views = db.FlightViews
                .Where(v => v.UserId == userId && v.ViewCount >= MinViewsForAlert)
                .ToList();

            int created = 0;
            foreach (var v in views)
            {
                var flight = db.Flights.Find(v.FlightId);
                if (flight != null && CheckView(db, v, flight)) created++;
            }
            if (created > 0) db.SaveChanges();
            return created;
        }

        // Tek bir kayıt için fiyat düşüşü kontrolü. Mesaj oluşturduysa true döner.
        // Mesaj oluşunca referans fiyat güncellenir -> aynı düşüş için tekrar mesaj gitmez.
        private static bool CheckView(TAContext db, FlightView view, Flight flight)
        {
            if (view.ViewCount < MinViewsForAlert) return false;
            if (!flight.IsActive || flight.DepartureTime < DateTime.Now) return false;

            bool created = false;

            if (view.LastSeenEconomyPrice > 0 && flight.EconomyPrice > 0 && flight.EconomyPrice < view.LastSeenEconomyPrice)
            {
                db.UserMessages.Add(BuildMessage(view.UserId, flight, "Ekonomi", "Economy", view.LastSeenEconomyPrice, flight.EconomyPrice, view.ViewCount));
                view.LastSeenEconomyPrice = flight.EconomyPrice;
                created = true;
            }

            if (view.LastSeenBusinessPrice > 0 && flight.BusinessPrice > 0 && flight.BusinessPrice < view.LastSeenBusinessPrice)
            {
                db.UserMessages.Add(BuildMessage(view.UserId, flight, "Business", "Business", view.LastSeenBusinessPrice, flight.BusinessPrice, view.ViewCount));
                view.LastSeenBusinessPrice = flight.BusinessPrice;
                created = true;
            }

            return created;
        }

        private static UserMessage BuildMessage(int userId, Flight f, string cabinTr, string cabinEn, decimal oldPrice, decimal newPrice, int views)
        {
            var tr = CultureInfo.GetCultureInfo("tr-TR");
            var enC = CultureInfo.GetCultureInfo("en-US");
            decimal diff = oldPrice - newPrice;
            int pct = oldPrice > 0 ? (int)Math.Round(diff / oldPrice * 100) : 0;
            string route = f.FromCode + " → " + f.ToCode;

            return new UserMessage
            {
                UserId = userId,
                Type = "PriceDrop",
                FlightId = f.FlightId,
                CabinClass = cabinEn,
                OldPrice = oldPrice,
                NewPrice = newPrice,
                IsRead = false,
                CreatedAt = DateTime.Now,

                TitleTr = "Fiyat düştü: " + route + " (" + f.FlightNumber + ")",
                TitleEn = "Price dropped: " + route + " (" + f.FlightNumber + ")",

                BodyTr = string.Format(tr,
                    "İlgilendiğiniz {0} seferinde {1} fiyatı düştü! {2:dd MMMM yyyy HH:mm} kalkışlı uçuş için fiyat {3:N0} ₺'den {4:N0} ₺'ye indi ({5:N0} ₺, %{6} indirim). Bu uçuşa {7} defa baktınız.",
                    f.FlightNumber, cabinTr, f.DepartureTime, oldPrice, newPrice, diff, pct, views),

                BodyEn = string.Format(enC,
                    "Good news! The {1} fare for flight {0} you were interested in has dropped. The {2:MMMM dd, yyyy HH:mm} flight is now ₺{4:N0} instead of ₺{3:N0} (₺{5:N0}, {6}% off). You viewed this flight {7} times.",
                    f.FlightNumber, cabinEn, f.DepartureTime, oldPrice, newPrice, diff, pct, views)
            };
        }
    }
}
