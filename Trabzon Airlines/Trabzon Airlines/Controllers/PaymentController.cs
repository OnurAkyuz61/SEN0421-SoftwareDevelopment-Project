using System; // DateTime ve Guid için
using System.Collections.Generic; // List koleksiyonları için
using System.Globalization; // Kültür ayarları için (Nokta/Virgül hatasını çözer)
using System.Linq; // LINQ sorguları için
using System.Text; // String işlemleri için
using System.Text.RegularExpressions; // Regex için
using System.Web.Mvc; // MVC controller için
using Iyzipay; // Iyzico SDK için
using Iyzipay.Model; // Iyzico model sınıfları için
using Iyzipay.Request; // Iyzico request sınıfları için
using Trabzon_Airlines.Models; // Model sınıfları için
using Trabzon_Airlines.Data; // Veritabanı bağlamı için

namespace Trabzon_Airlines.Controllers
{
    // Ödeme controller'ı - Iyzico ödeme işlemleri
    // Payment controller - Iyzico payment operations
    public class PaymentController : Controller
    {
        // Veritabanı bağlamı - TAContext sınıfından instance
        // Database context - Instance from TAContext class
        private TAContext db = new TAContext();
        // --------------------------
        // IYZICO AYARLARI
        // Iyzico Configuration
        // --------------------------
        // Iyzico API ayarlarını döndüren yardımcı metod
        // Helper method that returns Iyzico API settings
        private Options GetIyzicoOptions()
        {
            Options options = new Options();
            // Sandbox API anahtarı - Test ortamı için
            // Sandbox API key - For test environment
            options.ApiKey = "sandbox-8OnLjNCa7omCnhjjU008oi7BWaI6f22h";
            // Sandbox secret anahtarı - Test ortamı için
            // Sandbox secret key - For test environment
            options.SecretKey = "sandbox-XDhYUaKeZTRYekq5cYosV2HGxiQGMogD";
            // Sandbox base URL - Test ortamı adresi
            // Sandbox base URL - Test environment address
            options.BaseUrl = "https://sandbox-api.iyzipay.com";
            return options;
        }

        // --------------------------
        // INDEX - GET
        // Ödeme sayfası - GET metodu
        // --------------------------
        [HttpGet] // HTTP GET isteği için
        public ActionResult Index(decimal? amount)
        {
            // Session'dan bekleyen rezervasyon bilgilerini al
            // Get pending booking information from Session
            dynamic pendingBooking = Session["PendingBooking"];
            decimal finalAmount = amount ?? 1500m; // Varsayılan tutar - Default amount
            
            if (pendingBooking != null)
            {
                // Uçuş bilgilerini veritabanından al
                // Get flight information from database
                var flight = db.Flights.Find(pendingBooking.FlightId);
                if (flight != null)
                {
                    // Uçuş fiyatını kullan
                    // Use flight price
                    finalAmount = flight.Price;
                    
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
                    ViewBag.SeatNumber = pendingBooking.SeatNumber;
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
                }
            }
            
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
                var flight = db.Flights.Find(pendingBooking.FlightId);
                if (flight != null)
                {
                    finalAmount = flight.Price;
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
                    var flight = db.Flights.Find(pendingBooking.FlightId);
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
                        ViewBag.SeatNumber = pendingBooking.SeatNumber;
                    }
                }

                return View("Index", model);
            }

            try
            {
                // Fiyat Formatlama (En kritik nokta burasıdır)
                // Price Formatting (This is the most critical point)
                // Iyzico sadece "120.50" formatını kabul eder. Türkçe Windows'ta "120,50" olur ve hata verir.
                // Iyzico only accepts "120.50" format. On Turkish Windows it becomes "120,50" and causes error.
                string priceStr = model.Amount.ToString("F2", CultureInfo.InvariantCulture);

                // Iyzico ödeme isteği oluştur
                // Create Iyzico payment request
                CreatePaymentRequest request = new CreatePaymentRequest();
                request.Locale = Locale.TR.ToString(); // Türkçe dil desteği - Turkish language support
                request.ConversationId = Guid.NewGuid().ToString(); // Benzersiz konuşma ID'si - Unique conversation ID
                request.Price = priceStr; // Ödeme tutarı - Payment amount
                request.PaidPrice = priceStr; // Ödenen tutar - Paid amount
                request.Currency = Currency.TRY.ToString(); // Para birimi - Currency
                request.BasketId = "Bilet-" + DateTime.Now.Ticks; // Sepet ID'si - Basket ID
                request.PaymentGroup = PaymentGroup.PRODUCT.ToString(); // Ödeme grubu - Payment group

                // Callback URL (Dinamik port numarası için Request.Url kullanıyoruz)
                // Callback URL (Using Request.Url for dynamic port number)
                string baseUrl = Request.Url.Scheme + "://" + Request.Url.Authority;
                request.CallbackUrl = baseUrl + "/Payment/Callback";

                // Kart Bilgileri
                // Card Information
                PaymentCard paymentCard = new PaymentCard();
                paymentCard.CardHolderName = model.CardHolderName; // Kart sahibi adı - Card holder name
                paymentCard.CardNumber = model.CardNumber?.Replace(" ", "").Trim(); // Kart numarası (boşlukları temizle) - Card number (remove spaces)

                // Tarih Parçalama (Hata önleyici)
                // Date Parsing (Error prevention)
                if (model.ExpireDate != null && model.ExpireDate.Contains("/"))
                {
                    var dateParts = model.ExpireDate.Split('/');
                    paymentCard.ExpireMonth = dateParts[0].Trim(); // Ay - Month
                    paymentCard.ExpireYear = "20" + dateParts[1].Trim(); // Yıl (20XX formatı) - Year (20XX format)
                }
                else
                {
                    // Varsayılan test değeri (Formdan tarih gelmezse patlamasın diye)
                    // Default test value (so it doesn't crash if date doesn't come from form)
                    paymentCard.ExpireMonth = "12";
                    paymentCard.ExpireYear = "2028";
                }

                paymentCard.Cvc = model.Cvc; // CVC kodu - CVC code
                paymentCard.RegisterCard = 0; // Kartı kaydetme - Don't register card
                request.PaymentCard = paymentCard;

                // Müşteri Bilgileri (Sabit Test Verisi - Gerçek projede Session'dan alınmalı)
                // Customer Information (Fixed Test Data - Should be taken from Session in real project)
                Buyer buyer = new Buyer();
                buyer.Id = "BY789"; // Müşteri ID'si - Customer ID
                buyer.Name = "Test"; // Müşteri adı - Customer name
                buyer.Surname = "User"; // Müşteri soyadı - Customer surname
                buyer.GsmNumber = "+905350000000"; // Telefon numarası - Phone number
                buyer.Email = "test@email.com"; // E-posta adresi - Email address
                buyer.IdentityNumber = "11111111110"; // TC Kimlik No - Turkish ID number
                buyer.RegistrationAddress = "Test Adresi"; // Kayıt adresi - Registration address
                buyer.Ip = Request.UserHostAddress ?? "85.85.85.85"; // IP adresi - IP address
                buyer.City = "Istanbul"; // Şehir - City
                buyer.Country = "Turkey"; // Ülke - Country
                buyer.ZipCode = "34732"; // Posta kodu - Zip code
                request.Buyer = buyer;

                // Teslimat Adresi
                // Shipping Address
                Address shippingAddress = new Address();
                shippingAddress.ContactName = "Test User"; // İletişim adı - Contact name
                shippingAddress.City = "Istanbul"; // Şehir - City
                shippingAddress.Country = "Turkey"; // Ülke - Country
                shippingAddress.Description = "Test Adresi"; // Adres açıklaması - Address description
                shippingAddress.ZipCode = "34732"; // Posta kodu - Zip code
                request.ShippingAddress = shippingAddress;
                request.BillingAddress = shippingAddress; // Fatura adresi (teslimat adresi ile aynı) - Billing address (same as shipping address)

                // Sepet Öğeleri
                // Basket Items
                List<BasketItem> basketItems = new List<BasketItem>();
                BasketItem item = new BasketItem();
                item.Id = "BI101"; // Ürün ID'si - Product ID
                item.Name = "Ucak Bileti"; // Ürün adı - Product name
                item.Category1 = "Seyahat"; // Kategori - Category
                item.ItemType = BasketItemType.VIRTUAL.ToString(); // Ürün tipi (sanal) - Item type (virtual)
                item.Price = priceStr; // Ürün fiyatı - Product price
                basketItems.Add(item);
                request.BasketItems = basketItems;

                // Iyzico'ya İstek Gönder (3D Secure başlatma)
                // Send Request to Iyzico (3D Secure initialization)
                ThreedsInitialize threeDS = ThreedsInitialize.Create(request, GetIyzicoOptions()).Result;

                if (threeDS.Status == "success")
                {
                    // Iyzico HTML içeriğini direkt basar (3D Secure sayfası)
                    // Directly output Iyzico HTML content (3D Secure page)
                    return Content(threeDS.HtmlContent, "text/html");
                }
                else
                {
                    // Hata Detayı
                    // Error Details
                    ViewBag.Hata = "Iyzico Başlatma Hatası: " + threeDS.ErrorMessage + " (Hata Kodu: " + threeDS.ErrorCode + ")";
                    ViewBag.Error = "Iyzico Initialization Error: " + threeDS.ErrorMessage + " (Error Code: " + threeDS.ErrorCode + ")";
                    return View("Index", model);
                }
            }
            catch (Exception ex)
            {
                // Genel hata yakalama
                // General error handling
                ViewBag.Hata = "Sunucu Hatası: " + ex.Message;
                ViewBag.Error = "Server Error: " + ex.Message;
                return View("Index", model);
            }
        }

        // --------------------------
        // CALLBACK - POST
        // 3D Secure callback işlemi - POST metodu
        // --------------------------
        [HttpPost] // HTTP POST isteği için
        public ActionResult Callback()
        {
            try
            {
                // FormCollection yerine Request.Form kullanıyoruz (Daha güvenli)
                // Using Request.Form instead of FormCollection (More secure)
                string status = Request.Form["status"]; // İşlem durumu - Transaction status
                string paymentId = Request.Form["paymentId"]; // Ödeme ID'si - Payment ID
                string conversationId = Request.Form["conversationId"]; // Konuşma ID'si - Conversation ID
                string mdStatus = Request.Form["mdStatus"]; // 3D Secure durumu (1 ise doğrulama başarılı) - 3D Secure status (1 means verification successful)

                if (status != "success")
                {
                    // 3D Secure doğrulaması başarısız
                    // 3D Secure verification failed
                    ViewBag.Hata = "3D Secure Doğrulaması Yapılamadı.";
                    ViewBag.Error = "3D Secure Verification Failed.";
                    return View("Index", new PaymentViewModel());
                }

                // 2. Adım: Ödemeyi Tamamla (Provizyon Al)
                // Step 2: Complete Payment (Get Authorization)
                CreateThreedsPaymentRequest request = new CreateThreedsPaymentRequest();
                request.Locale = Locale.TR.ToString(); // Türkçe dil desteği - Turkish language support
                request.ConversationId = conversationId; // Konuşma ID'si - Conversation ID
                request.PaymentId = paymentId; // Ödeme ID'si - Payment ID

                // .Result ekleyerek "Task" yapısını kırıp direkt sonuca ulaşıyoruz
                // Breaking the "Task" structure by adding .Result to reach the result directly
                ThreedsPayment payment = ThreedsPayment.Create(request, GetIyzicoOptions()).Result;

                string gelenStatus = payment.Status?.ToLower() ?? ""; // İşlem durumu - Transaction status
                string gelenPaymentStatus = payment.PaymentStatus?.ToString().ToUpper() ?? ""; // Ödeme durumu - Payment status

                // IF bloğunu da yeni isimlere göre güncelledik
                // Updated IF block according to new names
                if (gelenStatus == "success" && (gelenPaymentStatus == "SUCCESS" || string.IsNullOrEmpty(gelenPaymentStatus)))
                {
                    // --- BAŞARILI BLOK ---
                    // --- SUCCESS BLOCK ---
                    // NOT: Iyzico bazen PaymentStatus'u boş gönderebiliyor ama Status success ise işlem başarılıdır.
                    // NOTE: Iyzico sometimes sends PaymentStatus empty but if Status is success, the transaction is successful.

                    // DATABASE İŞLEMİ - Rezervasyon oluşturma
                    // DATABASE OPERATION - Create booking
                    int? bookingId = null;
                    dynamic pendingBooking = Session["PendingBooking"];
                    if (pendingBooking != null)
                    {
                        try
                        {
                            // Benzersiz PNR kodu üret
                            // Generate unique PNR code
                            string pnr = GeneratePNR();

                            // Yeni rezervasyon oluştur
                            // Create new booking
                            var booking = new Booking
                            {
                                FlightId = pendingBooking.FlightId, // Uçuş ID'si - Flight ID
                                SeatId = pendingBooking.SeatId, // Koltuk ID'si - Seat ID
                                PassengerName = "Test", // Yolcu adı (Session'dan alınabilir) - Passenger name (can be taken from Session)
                                PassengerSurname = "User", // Yolcu soyadı (Session'dan alınabilir) - Passenger surname (can be taken from Session)
                                PNR = pnr, // PNR kodu - PNR code
                                BookingDate = DateTime.Now // Rezervasyon tarihi - Booking date
                            };

                            // Rezervasyonu veritabanına ekle
                            // Add booking to database
                            db.Bookings.Add(booking);
                            db.SaveChanges();

                            // Booking ID'yi sakla
                            // Save booking ID
                            bookingId = booking.BookingId;

                            // Session'dan bekleyen rezervasyon bilgisini temizle
                            // Clear pending booking information from Session
                            Session.Remove("PendingBooking");
                        }
                        catch (Exception dbEx)
                        {
                            // Veritabanı hatası - Loglama yapılabilir
                            // Database error - Logging can be done
                            System.Diagnostics.Debug.WriteLine("Rezervasyon oluşturma hatası: " + dbEx.Message);
                        }
                    }

                    ViewBag.Mesaj = "Tebrikler! Ödeme Başarılı. Ref: " + payment.PaymentId;
                    ViewBag.Message = "Congratulations! Payment Successful. Ref: " + payment.PaymentId;
                    ViewBag.PaymentId = payment.PaymentId; // Ödeme referans numarası - Payment reference number
                    ViewBag.BookingId = bookingId; // Rezervasyon ID'si (varsa) - Booking ID (if exists)
                    return View("success");
                }
                else
                {
                    // Bankadan onay geldi fakat ödeme alınamadı
                    // Approval came from bank but payment could not be received
                    ViewBag.Hata = "Bankadan onay geldi fakat ödeme alınamadı: " + payment.ErrorMessage;
                    ViewBag.Error = "Approval came from bank but payment could not be received: " + payment.ErrorMessage;
                    return View("error");
                }
            }
            catch (Exception ex)
            {
                // Callback işlem hatası
                // Callback operation error
                ViewBag.Hata = "Callback İşlem Hatası: " + ex.Message;
                ViewBag.Error = "Callback Operation Error: " + ex.Message;
                return View("Index", new PaymentViewModel());
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
            do
            {
                // 6 karakterlik rastgele PNR kodu üret
                // Generate 6-character random PNR code
                pnr = new string(
                    Enumerable.Repeat(chars, 6) // 6 karakter - 6 characters
                              .Select(s => s[random.Next(s.Length)]) // Her karakter için rastgele seç - Randomly select for each character
                              .ToArray()
                );
            }
            // Aynı PNR kodu veritabanında varsa tekrar üret
            // If same PNR code exists in database, generate again
            while (db.Bookings.Any(b => b.PNR == pnr));

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

