using System; // DateTime ve Guid için
using System.Collections.Generic; // List koleksiyonları için
using System.Data.Entity; // Entity Framework Include için
using System.Globalization; // Kültür ayarları için (Nokta/Virgül hatasını çözer)
using System.Linq; // LINQ sorguları için
using System.Text; // String işlemleri için
using System.Text.RegularExpressions; // Regex için
using System.Web.Mvc; // MVC controller için
using Trabzon_Airlines.Models; // Model sınıfları için
using Trabzon_Airlines.Data; // Veritabanı bağlamı için

namespace Trabzon_Airlines.Controllers
{
    // Ödeme controller'ı - Simüle edilmiş ödeme işlemleri
    // Payment controller - Simulated payment operations
    public class PaymentController : Controller
    {
        // Veritabanı bağlamı - TAContext sınıfından instance
        // Database context - Instance from TAContext class
        private TAContext db = new TAContext();

        // --- EKLENEN KISIM: EKSTRA HİZMET FİYATLARI (SABİT) ---
        private const decimal PRICE_EXTRA_BAGGAGE = 250.00m;
        private const decimal PRICE_WIFI = 150.00m;
        private const decimal PRICE_MEAL = 200.00m;
        private const decimal PRICE_FAST_TRACK = 300.00m;
        // -----------------------------------------------------

        // --------------------------
        // INDEX - GET
        // Ödeme sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Index(decimal? amount)
        {
            // Kullanıcı giriş kontrolü - Eğer giriş yapmamışsa giriş sayfasına yönlendir
            // User login check - If not logged in, redirect to login page
            if (Session["UserId"] == null)
            {
                TempData["Error_tr"] = "Ödeme işlemi için lütfen giriş yapın veya kayıt olun.";
                TempData["Error_en"] = "Please login or register to complete the payment.";
                string returnUrl = Url.Action("Index", "Payment", new { amount = amount });
                return RedirectToAction("Login", "Home", new { returnUrl = returnUrl });
            }
            // Session'dan bekleyen rezervasyon bilgilerini al
            // Get pending booking information from Session
            dynamic pendingBooking = Session["PendingBooking"];
            decimal finalAmount = amount ?? 1500m; // Varsayılan tutar - Default amount
            
            // Yolcu sayıları ve kabin sınıfı bilgilerini ViewBag'e ekle
            // Add passenger counts and cabin class information to ViewBag
            int adultCount = 1;
            int childCount = 0;
            int infantCount = 0;
            string cabinClass = "economy";
            
            if (pendingBooking != null)
            {
                // Session'dan yolcu sayılarını al
                // Get passenger counts from Session
                try
                {
                    adultCount = pendingBooking.AdultCount ?? 1;
                    childCount = pendingBooking.ChildCount ?? 0;
                    infantCount = pendingBooking.InfantCount ?? 0;
                    cabinClass = pendingBooking.CabinClass ?? "economy";
                }
                catch
                {
                    // Varsayılan değerler
                    // Default values
                }
                
                // Toplam fiyatı Session'dan al (zaten hesaplanmış)
                // Get total price from Session (already calculated)
                try
                {
                    if (pendingBooking.TotalPrice != null)
                    {
                        finalAmount = (decimal)pendingBooking.TotalPrice;
                    }
                }
                catch
                {
                    // Eğer TotalPrice yoksa, uçuş fiyatından hesapla
                    // If TotalPrice doesn't exist, calculate from flight price
                }
                
                // Uçuş bilgilerini veritabanından al
                // Get flight information from database
                int flightId = 0;
                try
                {
                    flightId = (int)pendingBooking.FlightId;
                }
                catch
                {
                    // FlightId alınamadıysa devam etme
                    // If FlightId cannot be retrieved, don't continue
                }
                var flight = db.Flights.Find(flightId);
                if (flight != null)
                {
                    // Eğer TotalPrice yoksa, uçuş fiyatından hesapla
                    // If TotalPrice doesn't exist, calculate from flight price
                    if (finalAmount == (amount ?? 1500m))
                    {
                        int totalPassengers = adultCount + childCount; // Infant genelde ücretsiz
                        try
                        {
                            decimal totalPrice = (decimal)pendingBooking.TotalPrice;
                            finalAmount = totalPrice * totalPassengers;
                        }
                        catch { }
                    }
                    
                    // Uçuş süresini hesapla (dakika cinsinden)
                    // Calculate flight duration (in minutes)
                    TimeSpan duration = flight.ArrivalTime - flight.DepartureTime;
                    int hours = (int)duration.TotalHours;
                    int minutes = duration.Minutes;
                    
                    // Uçuş bilgilerini ViewBag'e ekle
                    // Add flight information to ViewBag
                    ViewBag.FlightRoute = flight.FromCode + " → " + flight.ToCode;
                    ViewBag.FlightNumber = flight.FlightNumber;
                    ViewBag.DepartureDate = flight.DepartureTime.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));
                    ViewBag.DepartureTime = flight.DepartureTime.ToString("HH:mm", new System.Globalization.CultureInfo("tr-TR"));
                    ViewBag.ArrivalDate = flight.ArrivalTime.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));
                    ViewBag.ArrivalTime = flight.ArrivalTime.ToString("HH:mm", new System.Globalization.CultureInfo("tr-TR"));
                    ViewBag.FlightDuration = hours + "s " + minutes + "dk"; // Uçuş süresi - Flight duration
                    ViewBag.FromCode = flight.FromCode;
                    ViewBag.ToCode = flight.ToCode;
                    
                    // Çoklu koltuk seçimi kontrolü
                    // Multiple seat selection check
                    int[] selectedSeatIds = null;
                    try
                    {
                        selectedSeatIds = pendingBooking.SelectedSeatIds as int[];
                    }
                    catch { }
                    
                    if (selectedSeatIds != null && selectedSeatIds.Length > 0)
                    {
                        // Çoklu koltuk seçildiyse tüm koltuk numaralarını göster
                        // If multiple seats selected, show all seat numbers
                        var seatNumbers = new List<string>();
                        foreach (var seatId in selectedSeatIds)
                        {
                            var seat = db.Seats.Find(seatId);
                            if (seat != null)
                                seatNumbers.Add(seat.SeatNumber);
                        }
                        ViewBag.SeatNumber = string.Join(", ", seatNumbers);
                    }
                    else
                    {
                        // Tek koltuk seçimi (eski kod - geriye dönük uyumluluk)
                        // Single seat selection (old code - backward compatibility)
                        ViewBag.SeatNumber = pendingBooking.SeatNumber;
                    }
                    
                    // Yolcu bilgilerini ViewBag'e ekle
                    // Add passenger information to ViewBag
                    ViewBag.AdultCount = adultCount;
                    ViewBag.ChildCount = childCount;
                    ViewBag.InfantCount = infantCount;
                    ViewBag.CabinClass = cabinClass;
                    ViewBag.TotalPassengers = adultCount + childCount + infantCount;

                    // --- EKLENEN KISIM: Base Price'ı View'da kullanmak için gönderiyoruz ---
                    ViewBag.BasePrice = finalAmount;
                }
                else
                {
                    // Uçuş bulunamadıysa Session'dan al
                    // If flight not found, get from Session
                    ViewBag.FlightRoute = pendingBooking.FromCode + " → " + pendingBooking.ToCode;
                    ViewBag.FlightNumber = pendingBooking.FlightNumber;
                    DateTime depTime = (DateTime)pendingBooking.DepartureTime;
                    ViewBag.DepartureDate = depTime.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));
                    ViewBag.DepartureTime = depTime.ToString("HH:mm", new System.Globalization.CultureInfo("tr-TR"));
                    ViewBag.FromCode = pendingBooking.FromCode;
                    ViewBag.ToCode = pendingBooking.ToCode;
                    ViewBag.SeatNumber = pendingBooking.SeatNumber;
                    
                    // Yolcu bilgilerini ViewBag'e ekle
                    // Add passenger information to ViewBag
                    ViewBag.AdultCount = adultCount;
                    ViewBag.ChildCount = childCount;
                    ViewBag.InfantCount = infantCount;
                    ViewBag.CabinClass = cabinClass;
                    ViewBag.TotalPassengers = adultCount + childCount + infantCount;
                    
                    // Varış zamanı varsa göster
                    // Show arrival time if exists
                    if (pendingBooking.ArrivalTime != null)
                    {
                        DateTime arrTime = (DateTime)pendingBooking.ArrivalTime;
                        ViewBag.ArrivalDate = arrTime.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));
                        ViewBag.ArrivalTime = arrTime.ToString("HH:mm", new System.Globalization.CultureInfo("tr-TR"));
                        
                        // Uçuş süresini hesapla
                        // Calculate flight duration
                        TimeSpan duration = arrTime - depTime;
                        int hours = (int)duration.TotalHours;
                        int minutes = duration.Minutes;
                        ViewBag.FlightDuration = hours + "s " + minutes + "dk";
                    }
                    // --- EKLENEN KISIM: Base Price ---
                    ViewBag.BasePrice = finalAmount;
                }
            }
            else
            {
                // PendingBooking null ise BasePrice'ı finalAmount olarak set et
                // If PendingBooking is null, set BasePrice as finalAmount
                ViewBag.BasePrice = finalAmount;
            }

            // --- EKLENEN KISIM: EKSTRA HİZMET FİYATLARINI VIEW'A GÖNDER ---
            ViewBag.PriceBaggage = PRICE_EXTRA_BAGGAGE;
            ViewBag.PriceWifi = PRICE_WIFI;
            ViewBag.PriceMeal = PRICE_MEAL;
            ViewBag.PriceFastTrack = PRICE_FAST_TRACK;
            // -------------------------------------------------------------
            
            return View(new PaymentViewModel { Amount = finalAmount });
        }

        // --------------------------
        // PAY - POST
        // Ödeme işlemi - POST metodu
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        [ValidateAntiForgeryToken] // CSRF koruması için
        public ActionResult Pay(PaymentViewModel model)
        {
            // Model binding'i atla ve Amount'u manuel olarak set et
            // Skip model binding and set Amount manually
            // Bu, model binding sırasında oluşan "The value 'X' is not valid for Amount" hatasını önler
            // This prevents "The value 'X' is not valid for Amount" error during model binding
            
            // ModelState'den tüm Amount hatalarını temizle (önce temizle, sonra kontrol et)
            // Clear all Amount errors from ModelState (clear first, then check)
            ModelState.Remove("Amount");
            // ModelState'deki tüm Amount ile ilgili hataları da temizle
            // Clear all Amount-related errors in ModelState
            var amountErrors = ModelState.Where(x => x.Key.Contains("Amount")).ToList();
            foreach (var error in amountErrors)
            {
                ModelState.Remove(error.Key);
            }
            
            // Amount değerini belirle - Determine Amount value
            decimal finalAmount = 0;
            dynamic pendingBooking = Session["PendingBooking"];
            
            if (pendingBooking != null)
            {
                // Session'dan uçuş bilgisini al ve fiyatı kullan
                // Get flight information from Session and use price
                try
                {
                    // Önce TotalPrice'ı Session'dan almayı dene
                    // Try to get TotalPrice from Session first
                    try
                    {
                        if (pendingBooking.TotalPrice != null)
                        {
                            finalAmount = (decimal)pendingBooking.TotalPrice;
                        }
                    }
                    catch { }

                    // Eğer TotalPrice yoksa veya 0 ise, veritabanından almayı dene
                    // If TotalPrice doesn't exist or is 0, try to get from database
                    if (finalAmount <= 0)
                    {
                        try
                        {
                            int flightId = 0;
                            try
                            {
                                flightId = (int)pendingBooking.FlightId;
                            }
                            catch { }

                            if (flightId > 0)
                            {
                                var flight = db.Flights.Find(flightId);
                                if (flight != null)
                                {
                                    // CabinClass'a göre fiyat seç
                                    // Select price based on CabinClass
                                    string cabinClass = "economy";
                                    try
                                    {
                                        cabinClass = pendingBooking.CabinClass?.ToString() ?? "economy";
                                    }
                                    catch { }
                                    
                                    finalAmount = cabinClass.ToLower() == "business" 
                                        ? flight.BusinessPrice 
                                        : flight.EconomyPrice;
                                }
                            }
                        }
                        catch
                        {
                            // Veritabanı bağlantı hatası - Session'dan devam et
                            // Database connection error - Continue from Session
                        }
                    }

                    // --- EKLENEN KISIM: SERVER TARAFLI EKSTRA HESAPLAMA ---
                    // Kullanıcı JavaScript'i kapatsa bile sunucuda tekrar ekliyoruz
                    if (model.HasExtraBaggage) finalAmount += PRICE_EXTRA_BAGGAGE;
                    if (model.HasWifi) finalAmount += PRICE_WIFI;
                    if (model.HasMeal) finalAmount += PRICE_MEAL;
                    if (model.HasFastTrack) finalAmount += PRICE_FAST_TRACK;
                    // ------------------------------------------------------
                }
                catch
                {
                    // Hata durumunda Session'dan TotalPrice'ı almayı dene
                    // In case of error, try to get TotalPrice from Session
                    try
                    {
                        if (pendingBooking.TotalPrice != null)
                        {
                            finalAmount = (decimal)pendingBooking.TotalPrice;
                        }
                    }
                    catch { }
                }
            }
            
            // Eğer Session'dan alınamadıysa form'dan al - If not from Session, get from form
            if (finalAmount <= 0)
            {
                string amountStr = Request.Form["Amount"];
                if (string.IsNullOrEmpty(amountStr))
                {
                    // Model'den de dene (eğer model binding başarılı olduysa) - Try from model too (if model binding succeeded)
                    if (model.Amount > 0)
                    {
                        finalAmount = model.Amount;
                    }
                }
                else
                {
                    // Basit parse işlemi - Simple parse operation
                    // Virgülü noktaya çevir - Convert comma to dot
                    amountStr = amountStr.Replace(",", ".").Trim();
                    // Binlik ayırıcıları kaldır (sadece boşluk ve apostrophe) - Remove thousand separators (only spaces and apostrophes)
                    amountStr = amountStr.Replace(" ", "").Replace("'", "");
                    
                    // InvariantCulture ile parse et - Parse with InvariantCulture
                    if (decimal.TryParse(amountStr, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedAmount))
                    {
                        finalAmount = parsedAmount;
                    }
                }
            }
            
            // Amount'u model'e ata (model binding'i atladık) - Assign Amount to model (we skipped model binding)
            model.Amount = finalAmount;
            
            // Amount değer kontrolü - Amount value check
            if (model.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Tutar 0'dan büyük olmalıdır.");
            }
            else if (model.Amount > 999999999.99m)
            {
                ModelState.AddModelError("Amount", "Tutar çok büyük.");
            }

            // Kart numarasını temizle (boşlukları kaldır) - Manuel validasyon
            // Clean card number (remove spaces) - Manual validation
            // ModelState'den CardNumber hatasını temizle - Clear CardNumber error from ModelState
            ModelState.Remove("CardNumber");
            // ModelState'deki tüm CardNumber ile ilgili hataları da temizle
            // Clear all CardNumber-related errors in ModelState
            var cardNumberErrors = ModelState.Where(x => x.Key.Contains("CardNumber")).ToList();
            foreach (var error in cardNumberErrors)
            {
                ModelState.Remove(error.Key);
            }
            
            // Kart numarasını temizle - Clean card number
            string cleanedCardNumber = null;
            if (!string.IsNullOrEmpty(model.CardNumber))
            {
                // Tüm boşluk, tire ve diğer karakterleri kaldır - Remove all spaces, dashes and other characters
                cleanedCardNumber = model.CardNumber.Replace(" ", "").Replace("-", "").Replace("_", "").Trim();
            }
            else
            {
                // Form'dan da dene - Try from form too
                string cardNumberFromForm = Request.Form["CardNumber"];
                if (!string.IsNullOrEmpty(cardNumberFromForm))
                {
                    cleanedCardNumber = cardNumberFromForm.Replace(" ", "").Replace("-", "").Replace("_", "").Trim();
                }
            }
            
            // Temizlenmiş kart numarasını model'e ata - Assign cleaned card number to model
            model.CardNumber = cleanedCardNumber;
            
            // Kart numarası validasyonu - Card number validation
            if (string.IsNullOrWhiteSpace(model.CardNumber))
            {
                ModelState.AddModelError("CardNumber", "Kart numarası gereklidir.");
            }
            else if (model.CardNumber.Length < 13 || model.CardNumber.Length > 19)
            {
                ModelState.AddModelError("CardNumber", "Kart numarası 13-19 haneli olmalıdır.");
            }
            else if (!Regex.IsMatch(model.CardNumber, @"^\d+$"))
            {
                ModelState.AddModelError("CardNumber", "Kart numarası sadece rakam içermelidir.");
            }

            // Model validasyonu
            // Model validation
            if (!ModelState.IsValid)
            {
                // Sistemdeki tüm hataları toplayıp tek bir cümle yapıyoruz
                // Collect all errors in the system and make them a single sentence
                var hatalar = ModelState.Values.SelectMany(v => v.Errors)
                                               .Select(e => e.ErrorMessage).ToList();

                // Hataları ekrana basıyoruz
                // Display errors on screen
                ViewBag.Hata = "Hata Detayı: " + string.Join(" | ", hatalar);
                ViewBag.Error = "Error Details: " + string.Join(" | ", hatalar);

                // Session'dan uçuş bilgilerini tekrar yükle (pendingBooking zaten yukarıda tanımlı)
                // Reload flight information from Session (pendingBooking already defined above)
                if (pendingBooking == null)
                {
                    pendingBooking = Session["PendingBooking"];
                }
                if (pendingBooking != null)
                {
                    int flightId = 0;
                    try
                    {
                        flightId = (int)pendingBooking.FlightId;
                    }
                    catch { }
                    var flight = db.Flights.Find(flightId);
                    if (flight != null)
                    {
                        TimeSpan duration = flight.ArrivalTime - flight.DepartureTime;
                        int hours = (int)duration.TotalHours;
                        int minutes = duration.Minutes;
                        ViewBag.FlightRoute = flight.FromCode + " → " + flight.ToCode;
                        ViewBag.FlightNumber = flight.FlightNumber;
                        ViewBag.DepartureDate = flight.DepartureTime.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));
                        ViewBag.DepartureTime = flight.DepartureTime.ToString("HH:mm", new System.Globalization.CultureInfo("tr-TR"));
                        ViewBag.ArrivalDate = flight.ArrivalTime.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));
                        ViewBag.ArrivalTime = flight.ArrivalTime.ToString("HH:mm", new System.Globalization.CultureInfo("tr-TR"));
                        ViewBag.FlightDuration = hours + "s " + minutes + "dk";
                        ViewBag.FromCode = flight.FromCode;
                        ViewBag.ToCode = flight.ToCode;
                        
                        // Çoklu koltuk seçimi kontrolü
                        // Multiple seat selection check
                        int[] selectedSeatIds = null;
                        try
                        {
                            selectedSeatIds = pendingBooking.SelectedSeatIds as int[];
                        }
                        catch { }
                        
                        if (selectedSeatIds != null && selectedSeatIds.Length > 0)
                        {
                            // Çoklu koltuk seçildiyse tüm koltuk numaralarını göster
                            // If multiple seats selected, show all seat numbers
                            var seatNumbers = new List<string>();
                            foreach (var seatId in selectedSeatIds)
                            {
                                var seat = db.Seats.Find(seatId);
                                if (seat != null)
                                    seatNumbers.Add(seat.SeatNumber);
                            }
                            ViewBag.SeatNumber = string.Join(", ", seatNumbers);
                        }
                        else
                        {
                            // Tek koltuk seçimi (eski kod - geriye dönük uyumluluk)
                            // Single seat selection (old code - backward compatibility)
                            ViewBag.SeatNumber = pendingBooking.SeatNumber;
                        }
                    }
                }

                // --- EKLENEN KISIM: Hata durumunda Base Price tekrar gönder ---
                // Eğer bu olmazsa JS hesaplaması saçmalar
                if (pendingBooking != null)
                {
                    try
                    {
                        if (pendingBooking.TotalPrice != null)
                        {
                            decimal baseP = (decimal)pendingBooking.TotalPrice;
                            ViewBag.BasePrice = baseP;
                        }
                        else
                        {
                            ViewBag.BasePrice = finalAmount > 0 ? finalAmount : 1500m;
                        }
                    }
                    catch
                    {
                        try
                        {
                            int flightId = 0;
                            try
                            {
                                flightId = (int)pendingBooking.FlightId;
                            }
                            catch { }

                            if (flightId > 0)
                            {
                                var flight = db.Flights.Find(flightId);
                                if (flight != null)
                                {
                                    // CabinClass'a göre fiyat seç
                                    // Select price based on CabinClass
                                    string cabinClass = "economy";
                                    try
                                    {
                                        cabinClass = pendingBooking.CabinClass?.ToString() ?? "economy";
                                    }
                                    catch { }
                                    
                                    decimal flightPrice = cabinClass.ToLower() == "business" 
                                        ? flight.BusinessPrice 
                                        : flight.EconomyPrice;
                                    ViewBag.BasePrice = flightPrice;
                                }
                                else
                                {
                                    ViewBag.BasePrice = finalAmount > 0 ? finalAmount : 1500m;
                                }
                            }
                            else
                            {
                                ViewBag.BasePrice = finalAmount > 0 ? finalAmount : 1500m;
                            }
                        }
                        catch
                        {
                            ViewBag.BasePrice = finalAmount > 0 ? finalAmount : 1500m;
                        }
                    }
                }
                else
                {
                    ViewBag.BasePrice = finalAmount > 0 ? finalAmount : model.Amount;
                }

                // --- EKLENEN KISIM: Fiyatları tekrar gönder ---
                ViewBag.PriceBaggage = PRICE_EXTRA_BAGGAGE;
                ViewBag.PriceWifi = PRICE_WIFI;
                ViewBag.PriceMeal = PRICE_MEAL;
                ViewBag.PriceFastTrack = PRICE_FAST_TRACK;

                return View("Index", model);
            }

            try
            {
                // ÖDEME SİMÜLASYONU - Gerçek ödeme yapılmıyor, sadece simüle ediliyor
                // PAYMENT SIMULATION - No real payment is made, only simulated
                
                // Kullanıcı giriş kontrolü - Eğer giriş yapmamışsa hata ver
                // User login check - If not logged in, return error
                if (Session["UserId"] == null)
                {
                    ViewBag.Hata = "Ödeme işlemi için lütfen giriş yapın.";
                    ViewBag.Error = "Please login to complete the payment.";

                    // --- EKLENEN KISIM: Fiyatları tekrar gönder ---
                    ViewBag.PriceBaggage = PRICE_EXTRA_BAGGAGE;
                    ViewBag.PriceWifi = PRICE_WIFI;
                    ViewBag.PriceMeal = PRICE_MEAL;
                    ViewBag.PriceFastTrack = PRICE_FAST_TRACK;

                    return View("Index", model);
                }

                // Kısa bir gecikme simüle et (gerçek ödeme gibi görünsün)
                // Simulate a short delay (to look like a real payment)
                System.Threading.Thread.Sleep(1500);

                // Ödeme başarılı - Rezervasyon oluştur
                // Payment successful - Create booking
                // Not: pendingBooking zaten yukarıda tanımlı (satır 132)
                // Note: pendingBooking is already defined above (line 132)
                int? bookingId = null;
                
                if (pendingBooking != null)
                {
                    try
                    {
                        // Kullanıcı bilgilerini Session'dan al
                        // Get user information from Session
                        int userId = (int)Session["UserId"];
                        User user = null;
                        try
                        {
                            user = db.Users.Find(userId);
                        }
                        catch
                        {
                            // Veritabanı bağlantı hatası - devam et
                            // Database connection error - continue
                        }
                        
                        // Çoklu koltuk seçimi kontrolü
                        // Multiple seat selection check
                        int[] selectedSeatIds = null;
                        try
                        {
                            selectedSeatIds = pendingBooking.SelectedSeatIds as int[];
                        }
                        catch { }

                        // Eğer çoklu koltuk seçildiyse
                        // If multiple seats were selected
                        if (selectedSeatIds != null && selectedSeatIds.Length > 0)
                        {
                            // Her koltuk için ayrı booking oluştur
                            // Create separate booking for each seat
                            string basePnr = "PNR" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
                            try
                            {
                                basePnr = GeneratePNR(); // Tüm booking'ler için aynı PNR
                            }
                            catch
                            {
                                // GeneratePNR hatası - alternatif PNR kullan
                                // GeneratePNR error - use alternative PNR
                            }
                            
                            var firstBookingId = (int?)null;
                            
                            // FlightId'yi güvenli şekilde al (LINQ sorgusunda kullanmak için)
                            // Get FlightId safely (for use in LINQ query)
                            int bookingFlightId = 0;
                            string bookingCabinClass = "economy";
                            
                            try
                            {
                                bookingFlightId = (int)pendingBooking.FlightId;
                                bookingCabinClass = pendingBooking.CabinClass?.ToString() ?? "economy";
                            }
                            catch { }
                            
                            if (bookingFlightId == 0)
                            {
                                ViewBag.Hata = "Uçuş bilgisi bulunamadı.";
                                ViewBag.Error = "Flight information not found.";
                                return View("Index", model);
                            }

                            foreach (var seatId in selectedSeatIds)
                            {
                                try
                                {
                                    // Koltuk bilgisini al
                                    // Get seat information
                                    FlightSeat flightSeat = null;
                                    try
                                    {
                                        flightSeat = db.FlightSeats
                                                      .Include(x => x.Seat)
                                                      .FirstOrDefault(x => x.FlightId == bookingFlightId && x.SeatId == seatId);
                                    }
                                    catch
                                    {
                                        // Veritabanı bağlantı hatası - devam et
                                        // Database connection error - continue
                                        continue;
                                    }
                                    
                                    if (flightSeat == null)
                                        continue;

                                    // Koltuk sınıfına göre fiyat belirle
                                    // Determine price based on seat class
                                    decimal seatPrice = 0;
                                    try
                                    {
                                        seatPrice = flightSeat.Seat.CabinClass == "Business"
                                            ? flightSeat.Flight.BusinessPrice
                                            : flightSeat.Flight.EconomyPrice;
                                    }
                                    catch
                                    {
                                        // Session'dan TotalPrice'ı kullan
                                        // Use TotalPrice from Session
                                        try
                                        {
                                            if (pendingBooking.TotalPrice != null)
                                            {
                                                seatPrice = (decimal)pendingBooking.TotalPrice;
                                            }
                                        }
                                        catch { }
                                    }

                                    // Yeni rezervasyon oluştur
                                    // Create new booking
                                    var booking = new Booking
                                    {
                                        FlightId = bookingFlightId,
                                        SeatId = seatId,
                                        PassengerName = user?.FirstName ?? "Bilinmeyen",
                                        PassengerSurname = user?.LastName ?? "Kullanıcı",
                                        PNR = basePnr, // Tüm booking'ler aynı PNR'ye sahip
                                        BookingDate = DateTime.Now,
                                        Price = seatPrice > 0 ? seatPrice : finalAmount,
                                        CabinClass = bookingCabinClass
                                    };

                                    db.Bookings.Add(booking);
                                    
                                    // İlk booking ID'sini sakla (meal selection için)
                                    // Save first booking ID (for meal selection)
                                    if (!firstBookingId.HasValue)
                                    {
                                        try
                                        {
                                            db.SaveChanges(); // İlk booking'i kaydet
                                            firstBookingId = booking.BookingId;
                                        }
                                        catch
                                        {
                                            // Veritabanı kayıt hatası - devam et
                                            // Database save error - continue
                                        }
                                    }
                                }
                                catch
                                {
                                    // Koltuk işleme hatası - devam et
                                    // Seat processing error - continue
                                    continue;
                                }
                            }
                            
                            // Kalan booking'leri kaydet
                            // Save remaining bookings
                            try
                            {
                                db.SaveChanges();
                            }
                            catch
                            {
                                // Veritabanı kayıt hatası - devam et
                                // Database save error - continue
                            }
                            
                            bookingId = firstBookingId;
                        }
                        else
                        {
                            // Tek koltuk seçimi (eski kod - geriye dönük uyumluluk)
                            // Single seat selection (old code - backward compatibility)
                            string pnr = "PNR" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
                            try
                            {
                                pnr = GeneratePNR();
                            }
                            catch
                            {
                                // GeneratePNR hatası - alternatif PNR kullan
                                // GeneratePNR error - use alternative PNR
                            }

                            // FlightId'yi güvenli şekilde al
                            // Get FlightId safely
                            int singleFlightId = 0;
                            int singleSeatId = 0;
                            decimal singleTotalPrice = finalAmount;
                            string singleCabinClass = "economy";
                            
                            try
                            {
                                singleFlightId = (int)pendingBooking.FlightId;
                                singleSeatId = (int)pendingBooking.SeatId;
                                if (pendingBooking.TotalPrice != null)
                                {
                                    singleTotalPrice = (decimal)pendingBooking.TotalPrice;
                                }
                                singleCabinClass = pendingBooking.CabinClass?.ToString() ?? "economy";
                            }
                            catch { }
                            
                            var booking = new Booking
                            {
                                FlightId = singleFlightId,
                                SeatId = singleSeatId,
                                PassengerName = user?.FirstName ?? "Bilinmeyen",
                                PassengerSurname = user?.LastName ?? "Kullanıcı",
                                PNR = pnr,
                                BookingDate = DateTime.Now,
                                Price = singleTotalPrice > 0 ? singleTotalPrice : finalAmount,
                                CabinClass = singleCabinClass
                            };

                            try
                            {
                                db.Bookings.Add(booking);
                                db.SaveChanges();
                                bookingId = booking.BookingId;
                            }
                            catch
                            {
                                // Veritabanı kayıt hatası - bookingId null kalacak, success sayfasına gidecek
                                // Database save error - bookingId will remain null, will go to success page
                            }
                        }

                        // Session'dan bekleyen rezervasyon bilgisini temizle
                        // Clear pending booking information from Session
                        Session.Remove("PendingBooking");
                    }
                    catch (Exception dbEx)
                    {
                        // Veritabanı hatası - Loglama yapılabilir
                        // Database error - Logging can be done
                        System.Diagnostics.Debug.WriteLine("Rezervasyon oluşturma hatası: " + dbEx.Message);
                        ViewBag.Hata = "Rezervasyon oluşturulurken bir hata oluştu: " + dbEx.Message;
                        ViewBag.Error = "An error occurred while creating the booking: " + dbEx.Message;

                        // --- EKLENEN KISIM: Fiyatları tekrar gönder ---
                        ViewBag.PriceBaggage = PRICE_EXTRA_BAGGAGE;
                        ViewBag.PriceWifi = PRICE_WIFI;
                        ViewBag.PriceMeal = PRICE_MEAL;
                        ViewBag.PriceFastTrack = PRICE_FAST_TRACK;

                        return View("Index", model);
                    }
                }

                // Ödeme başarılı - MealSelection sayfasına yönlendir
                // Payment successful - Redirect to MealSelection page
                if (bookingId.HasValue)
                {
                    // MealSelection sayfasına yönlendir
                    // Redirect to MealSelection page
                    return RedirectToAction("MealSelection", "Booking", new { bookingId = bookingId.Value });
                }
                
                // Eğer bookingId yoksa success sayfasına git
                // If bookingId doesn't exist, go to success page
                string paymentId = "SIM-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
                ViewBag.Mesaj = "Tebrikler! Ödeme Başarılı. Ref: " + paymentId;
                ViewBag.Message = "Congratulations! Payment Successful. Ref: " + paymentId;
                ViewBag.PaymentId = paymentId; // Ödeme referans numarası - Payment reference number
                ViewBag.BookingId = bookingId; // Rezervasyon ID'si (varsa) - Booking ID (if exists)
                return View("success");
            }
            catch (Exception ex)
            {
                // Genel hata yakalama
                // General error handling
                ViewBag.Hata = "Sunucu Hatası: " + ex.Message;
                ViewBag.Error = "Server Error: " + ex.Message;

                // --- EKLENEN KISIM: Fiyatları tekrar gönder ---
                ViewBag.PriceBaggage = PRICE_EXTRA_BAGGAGE;
                ViewBag.PriceWifi = PRICE_WIFI;
                ViewBag.PriceMeal = PRICE_MEAL;
                ViewBag.PriceFastTrack = PRICE_FAST_TRACK;

                return View("Index", model);
            }
        }


        // --------------------------
        // PNR ÜRETİCİ YARDIMCI FONKSİYON
        // PNR generator helper function
        // --------------------------
        private string GeneratePNR()
        {
            // PNR için kullanılacak karakterler
            // Characters to use for PNR
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random(); // Rastgele sayı üretici - Random number generator

            string pnr; // PNR değişkeni - PNR variable
            int maxAttempts = 100; // Maksimum deneme sayısı - Maximum attempt count
            int attempt = 0;
            
            do
            {
                // 6 karakterlik rastgele PNR kodu üret
                // Generate 6-character random PNR code
                pnr = new string(
                    Enumerable.Repeat(chars, 6) // 6 karakter - 6 characters
                              .Select(s => s[random.Next(s.Length)]) // Her karakter için rastgele seç - Randomly select for each character
                              .ToArray()
                );
                
                attempt++;
                
                // Veritabanı kontrolü - Database check
                bool exists = false;
                try
                {
                    exists = db.Bookings.Any(b => b.PNR == pnr);
                }
                catch
                {
                    // Veritabanı bağlantı hatası - PNR'yi kabul et
                    // Database connection error - accept PNR
                    break;
                }
                
                // Eğer PNR yoksa veya maksimum deneme sayısına ulaşıldıysa çık
                // If PNR doesn't exist or max attempts reached, exit
                if (!exists || attempt >= maxAttempts)
                {
                    break;
                }
            }
            while (true);

            // Üretilen PNR kodunu döndür
            // Return generated PNR code
            return pnr;
        }

        // --------------------------
        // DISPOSE - KAYNAKLARI TEMİZLE
        // Dispose - Clean up resources
        // --------------------------
        protected override void Dispose(bool disposing)
        {
            // Eğer disposing true ise veritabanı bağlantısını kapat
            // If disposing is true, close database connection
            if (disposing)
            {
                db.Dispose(); // Veritabanı bağlantısını kapat - Close database connection
            }

            // Base dispose metodunu çağır
            // Call base dispose method
            base.Dispose(disposing);
        }
    }
}

