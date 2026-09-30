# 🛫 Trabzon Airlines - Havayolu Rezervasyon Sistemi

## 📋 Proje Bilgileri

**Üniversite:** İstanbul Kültür Üniversitesi  
**Ders:** SEN0421 - Software Development with Standards  
**Dönem:** 2026-2027 Bahar Dönemi  
**Proje Konusu:** Airline Reservation System (Havayolu Rezervasyon Sistemi)  
**Teknoloji:** ASP.NET MVC (.NET Framework)  
**Proje Durumu:** 🚧 **DEVAM EDİYOR** (henüz tamamlanmadı)

---

## 🚧 Proje Durumu

Bu proje **henüz tamamlanmamıştır**; geliştirme ve iyileştirme çalışmaları devam etmektedir. Aşağıdaki özelliklerin büyük kısmı çalışır durumda olsa da teslim / final sürümü değildir.

### ✅ Şu Ana Kadar Hazır Olanlar
- ✅ Uçuş arama ve rezervasyon sistemi
- ✅ Online check-in sistemi
- ✅ Koltuk seçimi
- ✅ Yemek seçimi
- ✅ Ödeme simülasyonu
- ✅ Admin paneli
- ✅ Kullanıcı yönetimi
- ✅ Çok dilli destek (TR/EN)
- ✅ Responsive tasarım
- ✅ Veritabanı entegrasyonu
- ✅ AI sohbet kutusu / mesajlar (User Secrets ile API anahtarı)
- ✅ Hava durumu entegrasyonu
- ✅ Hata yönetimi
- ✅ Güvenlik önlemleri (secret'lar repo dışında)

---

## 👥 Ekip Üyeleri

| Öğrenci No | Ad Soyad |
|------------|----------|
| 2100005226 | Emirhan Mollahüseyinoğlu |
| 2200005590 | Onur Akyüz |
| 2300006496 | Rana Neslihan Kurt |
| 2300007578 | Zeynep Avcı |

## 🎯 Proje Açıklaması

Trabzon Airlines, ASP.NET MVC (.NET Framework) kullanılarak geliştirilmiş kapsamlı bir havayolu rezervasyon sistemidir. Proje, kullanıcıların uçuş arama, rezervasyon yapma, hesap yönetimi ve çeşitli havayolu hizmetlerine erişim sağlayabileceği responsive bir web uygulamasıdır.

### ✨ Özellikler

- 🔍 **Uçuş Arama ve Rezervasyon** ✅
  - Gelişmiş arama filtreleri (Gidiş-Dönüş, Tek Yön, Çoklu Uçuş)
  - Havalimanı seçim modal'ı
  - Yolcu ve kabin sınıfı seçimi
  - Tarih validasyonu
  - Promosyon kodu desteği
  - **Veritabanı entegrasyonu ile gerçek zamanlı uçuş arama**
  - **Uçuş sonuçları sayfası (SearchResults)**
  - **Uçuş bulunamadığında popup modal uyarısı**
  - **AJAX ile asenkron arama işlemi**
  - **Loading durumu göstergesi**
  - **Koltuk seçimi (SelectSeat)** - Uçuş sonrası koltuk seçimi
  - **Yemek seçimi (MealSelection)** - Uçuş sırasında yemek siparişi
  - **Ödeme sistemi (Payment)** - Simüle edilmiş ödeme sistemi (gerçek ödeme yapılmaz)
  - **Kullanıcı giriş kontrolü** - Koltuk seçiminden sonra ödeme için giriş zorunluluğu

- 🎫 **Online Check-In Sistemi** ✅
  - **PNR ile rezervasyon arama** - PNR kodu ile rezervasyon bulma
  - **Rezervasyon detayları** - Uçuş, yolcu, koltuk ve yemek bilgileri
  - **Check-in işlemi** - 24 saat kuralı ile check-in yapma
  - **Biniş kartı (Boarding Pass)** - Yazdırılabilir biniş kartı
  - **Check-in durumu kontrolü** - Zaten check-in yapılmış mı kontrolü
  - **Tarih validasyonu** - Geçmiş uçuşlar için check-in engelleme
  - **Veritabanı bağlantı kontrolü** - Güvenli veritabanı bağlantı yönetimi
  - **Hata yönetimi** - Kapsamlı hata yakalama ve kullanıcı bilgilendirme

- 🌍 **Çok Dilli Destek** ✅
  - Türkçe ve İngilizce dil seçenekleri
  - Dinamik dil değiştirme
  - Sayfa geçişlerinde dil korunması
  - Para birimi seçimi (₺, US$, €, £)

- 📱 **Responsive Tasarım** ✅
  - Mobil uyumlu arayüz
  - Mobil alt navigasyon çubuğu
  - Tablet ve desktop optimizasyonu
  - Modern UI/UX tasarımı
  - Sidebar navigasyon sistemi

- 🔐 **Kullanıcı Yönetimi**
  - Kayıt ol (Sign Up) sayfası
  - Giriş yap (Login) sayfası
  - Şifre sıfırlama
  - TC Kimlik validasyonu
  - Şifre gücü göstergesi
  - **Zorunlu giriş kontrolü** - Rezervasyon işlemleri için giriş zorunluluğu
  - **ReturnUrl desteği** - Giriş sonrası önceki sayfaya geri dönüş

- 📦 **Paket ve Fırsat Yönetimi**
  - Paket listeleme ve detay sayfaları
  - Fırsat listeleme ve detay sayfaları
  - Paket rezervasyon sistemi
  - Fırsat rezervasyon sistemi
  - Aktif/Pasif paket yönetimi
  - Öne çıkan paket/fırsat işaretleme

- 👨‍💼 **Admin Paneli** ✅
  - **Admin Giriş Sistemi** - Veritabanına bağlı güvenli giriş
  - **Admin Layout** - Özel navbar, sidebar ve footer tasarımı
  - **Dashboard** - Gerçek zamanlı istatistikler (Toplam Uçuş, Rezervasyon, Kullanıcı, vb.)
  - **Uçuş Yönetimi** - Uçuş listeleme, ekleme, düzenleme, silme
  - **Rezervasyon Yönetimi** - Rezervasyon listeleme, detay görüntüleme, silme
  - **Kullanıcı Yönetimi** - Kullanıcı listeleme, detay görüntüleme, silme
  - **Ayarlar** - Profil düzenleme, şifre değiştirme
  - **Session Kontrolü** - Tüm admin sayfalarında oturum kontrolü
  - **Responsive Tasarım** - Mobil uyumlu admin paneli
  - **Ayrı CSS/JS Dosyaları** - Her sayfa için özel stil ve script dosyaları

- 💳 **Ödeme Sistemi** ✅
  - **Simüle edilmiş ödeme** - Gerçek ödeme yapılmaz, sadece simüle edilir
  - **Kredi kartı ödeme formu** - İnteraktif kart gösterimi
  - **Dinamik kart UI** - Gerçek zamanlı kart bilgisi gösterimi
  - **Para birimi dönüşümü** - TL, USD, EUR, GBP desteği
  - **Uçuş bilgisi entegrasyonu** - Ödeme sayfasında uçuş detayları
  - **Session yönetimi** - Rezervasyon bilgilerinin güvenli aktarımı
  - **Ödeme başarı sayfası** - Başarılı ödeme sonrası rezervasyon oluşturma
  - **Kullanıcı giriş kontrolü** - Ödeme sayfasına erişim için giriş zorunluluğu

- 📄 **Sayfalar**
  - Ana Sayfa (Index) - Uçuş arama ve öne çıkan teklifler
  - Hakkımızda (About) - Şirket bilgileri, misyon, vizyon, ekip
  - İletişim (Contact) - İletişim formu ve FAQ (KVKK ve Gizlilik Politikası linkleri)
  - Destek (Support) - Müşteri destek formu ve geçmiş talepler
  - Giriş Yap (Login) - Kullanıcı girişi
  - Kayıt Ol (Signup) - Yeni kullanıcı kaydı
  - Paketler (Packages) - Tüm paketleri listeleme
  - Paket Detay (PackageDetail) - Paket detay sayfası
  - Fırsatlar (Deals) - Tüm fırsatları listeleme
  - Fırsat Detay (DealDetail) - Fırsat detay sayfası
  - Ayarlar (Settings) - Kullanıcı profil ve ayar yönetimi
  - Site Haritası (Sitemap) - Site yapısı (Admin sayfaları tek kategori altında)
  - Gizlilik Politikası (Privacy) - KVKK ve gizlilik bilgileri
  - **KVKK Aydınlatma Metni (KVKK)** - 6698 sayılı KVKK kapsamında aydınlatma metni
  - **Trip AI (TripAI)** - Akıllı seyahat planlayıcı
  - **Uçuş Listesi (Flights/Index)** - Modern kart tasarımı ile aktif uçuş listesi
  - **Uçuş Durumu (Flights/Status)** - Uçuş durum bilgileri ve rötar riski
  - **Koltuk Seçimi (Booking/SelectSeat)** - Uçuş sonrası koltuk seçimi (giriş zorunlu)
  - **Güvenli Ödeme (Payment/Index)** - Simüle edilmiş ödeme sayfası (giriş zorunlu)
  - **Ödeme Başarı (Payment/success)** - Başarılı ödeme sonrası rezervasyon oluşturma
  - **Rezervasyon Sonucu (Booking/BookingResult)** - Rezervasyon onay sayfası
  - **Yemek Seçimi (Booking/MealSelection)** - Uçuş yemekleri seçimi
  - **Online Check-In (CheckIn/Index)** - PNR ile check-in sayfası
  - **Rezervasyon Detayları (CheckIn/BookingDetails)** - Check-in için rezervasyon detayları
  - **Biniş Kartı (CheckIn/BoardingPass)** - Yazdırılabilir biniş kartı
  - **404 Sayfa Bulunamadı (Error/NotFound)** - Özel 404 hata sayfası

- 🔐 **Admin Sayfaları** ✅
  - **Admin Giriş (Admin/Login)** - Admin paneli giriş sayfası
  - **Admin Dashboard (Admin/Dashboard)** - İstatistikler ve özet bilgiler
  - **Uçuş Yönetimi (AdminFlights/Index)** - Tüm uçuşları listeleme
  - **Yeni Uçuş Ekle (AdminFlights/Create)** - Yeni uçuş ekleme formu
  - **Uçuş Düzenle (AdminFlights/Edit)** - Uçuş bilgilerini düzenleme
  - **Uçuş Sil (AdminFlights/Delete)** - Uçuş silme işlemi
  - **Rezervasyon Yönetimi (AdminBookings/Index)** - Tüm rezervasyonları listeleme
  - **Rezervasyon Detayı (AdminBookings/Details)** - Rezervasyon detay görüntüleme
  - **Kullanıcı Yönetimi (AdminUsers/Index)** - Tüm kullanıcıları listeleme
  - **Kullanıcı Detayı (AdminUsers/Details)** - Kullanıcı detay görüntüleme
  - **Admin Yönetimi (AdminAdmins/Index)** - Admin kullanıcıları listeleme
  - **Yeni Admin Ekle (AdminAdmins/Create)** - Yeni admin ekleme
  - **Admin Düzenle (AdminAdmins/Edit)** - Admin bilgilerini düzenleme
  - **Destek Talepleri (AdminSupport/Index)** - Müşteri destek taleplerini yönetme
  - **Ayarlar (Admin/Settings)** - Admin profil ve şifre yönetimi

## 🛠️ Kullanılan Teknolojiler

### Backend
- **ASP.NET MVC 5** - Model-View-Controller mimarisi
- **.NET Framework 4.7.2** - Framework versiyonu
- **C#** - Programlama dili
- **Razor View Engine** - View rendering
- **Bundle & Minification** - CSS/JS optimizasyonu
- **Entity Framework 6.5.1** - ORM (Object-Relational Mapping)
- **SQL Server LocalDB** - Veritabanı (MSSQLLocalDB)
- **SHA256** - Şifre hash'leme algoritması
- **Session Management** - Kullanıcı oturum yönetimi
- **Ödeme Simülasyonu** - Gerçek ödeme yapılmadan rezervasyon oluşturma

### Frontend
- **HTML5** - Semantik yapı ve erişilebilirlik
- **CSS3** - Modern styling, animasyonlar ve responsive tasarım
  - Custom CSS dosyaları:
    - style.css - Ana stil dosyası
    - about.css - Hakkımızda sayfası
    - auth.css - Giriş/Kayıt sayfaları
    - contact.css - İletişim sayfası
    - support.css - Destek sayfası
    - trip.css - Trip AI sayfası
    - flight-list.css - Uçuş listesi sayfası
    - flight-status.css - Uçuş durumu sayfası
    - admin-layout.css - Admin paneli layout
    - admin-dashboard.css - Admin dashboard
    - admin-flights.css - Admin uçuş yönetimi
    - admin-bookings.css - Admin rezervasyon yönetimi
    - admin-users.css - Admin kullanıcı yönetimi
    - ve diğer admin sayfaları için özel CSS dosyaları
  - Bootstrap 5 - Grid sistemi ve utilities
- **JavaScript (ES6+)** - İnteraktif özellikler ve DOM manipülasyonu
  - airports.js - Havalimanı listesi ve seçim
  - language.js - Dil yönetimi
  - sidebar.js - Sidebar kontrolü
  - flight-search.js - Uçuş arama fonksiyonları
  - forms.js - Form validasyonu
  - alerts.js - Uyarı mesajları
  - multi-city.js - Çoklu uçuş yönetimi
  - trip-type.js - Seyahat türü yönetimi
  - tripai.js - Trip AI fonksiyonları
  - main.js - Ana initialization
  - auth.js - Kimlik doğrulama
- **Font Awesome 6.5.0** - İkon kütüphanesi
- **jQuery 3.7.0** - DOM manipülasyonu ve AJAX
- **CSS Grid & Flexbox** - Modern layout sistemleri

### SEO ve Erişilebilirlik
- **Meta Tags** - Arama motoru optimizasyonu
- **Open Graph** - Sosyal medya entegrasyonu
- **Twitter Cards** - Twitter paylaşım optimizasyonu
- **Semantic HTML** - Erişilebilirlik ve SEO
- **ARIA Labels** - Screen reader desteği

### Araçlar ve Platformlar
- **Visual Studio 2022** - Geliştirme ortamı
- **Git & GitHub** - Versiyon kontrolü
- **NuGet** - Paket yönetimi
- **IIS Express** - Yerel web sunucusu

## 📁 Detaylı Proje Yapısı

```
SEN0421-SoftwareDevelopment-Project/
├── .gitignore
├── README.md
└── Trabzon Airlines/
    ├── Trabzon Airlines.sln                 # Visual Studio solution
    ├── packages/                            # NuGet paketleri (gitignore; restore ile gelir)
    └── Trabzon Airlines/                    # ASP.NET MVC 5 projesi
        ├── App_Start/                       # Bundle, Filter, Route config
        ├── Controllers/                     # 17 controller
        │   ├── HomeController.cs            # Ana sayfalar + iletişim formu
        │   ├── AccountController.cs         # Kullanıcı hesabı
        │   ├── FlightsController.cs         # Uçuş listesi / arama / durum
        │   ├── BookingController.cs         # Koltuk, yemek, rezervasyon sonucu
        │   ├── CheckInController.cs         # Online check-in + boarding pass
        │   ├── PaymentController.cs         # Ödeme simülasyonu
        │   ├── ChatController.cs            # AI sohbet API
        │   ├── MessagesController.cs        # Kullanıcı mesajları
        │   ├── SupportController.cs         # Destek talepleri
        │   ├── SitemapController.cs
        │   ├── ErrorController.cs
        │   ├── AdminController.cs           # Admin giriş / dashboard / ayarlar
        │   ├── AdminFlightsController.cs
        │   ├── AdminBookingsController.cs
        │   ├── AdminUsersController.cs
        │   ├── AdminAdminsController.cs
        │   └── AdminSupport.cs
        ├── Models/                          # 17 model / view-model
        │   ├── User.cs, Admin.cs, Flight.cs, Seat.cs, FlightSeat.cs
        │   ├── Booking.cs, BookingMeal.cs, Meal.cs, SupportRequest.cs
        │   ├── FlightView.cs, FlightWeather.cs, RouteSearch.cs, UserMessage.cs
        │   └── PaymentViewModel.cs, AccountViewModels.cs, Recent*.cs
        ├── Data/
        │   └── TAContext.cs                 # EF6 DbContext
        ├── Services/                        # İş mantığı servisleri
        │   ├── AiChatService.cs
        │   ├── WeatherService.cs
        │   └── PriceWatchService.cs
        ├── Views/                           # ~51 Razor view
        │   ├── Home/, Flights/, Booking/, CheckIn/, Payment/
        │   ├── Account/, Messages/
        │   ├── Admin/, AdminFlights/, AdminBookings/, AdminUsers/
        │   ├── AdminAdmins/, AdminSupport/
        │   └── Shared/                      # _Layout, _AdminLayout, _ChatWidget, Error
        ├── Content/
        │   ├── css/                         # ~29 özel CSS (chatbot, messages dahil)
        │   └── bootstrap*.css, Site.css, SeatMap.css
        ├── Scripts/                         # jQuery, Bootstrap, validation
        ├── js/                              # ~21 özel JS (chatbot.js dahil)
        ├── assets/                          # images (destinations, meals, team), logo
        ├── Migrations/                      # EF Code First migrations + Configuration.cs
        ├── App_Data/                        # LocalDB dosyaları (gitignore: *.mdf/*.ldf)
        ├── Properties/
        ├── Global.asax / Global.asax.cs
        ├── Web.config                       # TAConn + appSettings (secret'lar boş)
        ├── secrets.example.xml              # User Secrets şablonu (repo'da örnek)
        ├── packages.config
        └── Trabzon Airlines.csproj
```

### 📊 Dosya İstatistikleri

- **Controller:** 17
- **Model:** 17
- **View (.cshtml):** ~51
- **Özel CSS:** ~29 (+ Bootstrap)
- **Özel JavaScript:** ~21 (+ jQuery/Bootstrap)
- **Services:** 3
- **Migrations:** Configuration + migration setleri

> `bin/`, `obj/`, `.vs/`, `packages/` ve `App_Data/*.mdf` Git'e eklenmez (`.gitignore`).

## 🚀 Kurulum ve Çalıştırma

### Gereksinimler
- **Visual Studio 2022** (önerilen) veya Visual Studio 2019
- **.NET Framework 4.7.2** veya üzeri
- **SQL Server LocalDB** (VS ile gelir)
- **IIS Express** (VS ile gelir)
- Modern tarayıcı (Chrome, Firefox, Safari, Edge)
- (Opsiyonel) AI sohbet / SMTP için User Secrets değerleri

### Kurulum Adımları

1. **Projeyi klonlayın:**
   ```bash
   git clone https://github.com/OnurAkyuz61/SEN0421-SoftwareDevelopment-Project.git
   cd SEN0421-SoftwareDevelopment-Project
   ```

2. **Visual Studio'da açın:**
   - `Trabzon Airlines/Trabzon Airlines.sln` dosyasını açın
   - NuGet paketlerinin restore edilmesini bekleyin (`packages/` klasörü otomatik oluşur)

3. **User Secrets ayarlayın (AI sohbet + e-posta için zorunlu):**
   - Şablon: `Trabzon Airlines/Trabzon Airlines/secrets.example.xml`
   - Dosyayı şu konuma kopyalayın ve gerçek değerleri yazın:
     - **Windows:** `%APPDATA%\Microsoft\UserSecrets\fa7a7c3c-433f-49bc-b800-debf86b94b2a\secrets.xml`
     - **macOS / Linux:** `~/.config/Microsoft/UserSecrets/fa7a7c3c-433f-49bc-b800-debf86b94b2a/secrets.xml`
   - Doldurulacak anahtarlar: `AI:ApiKey`, `Smtp:UserName`, `Smtp:Password`
   - Bu dosya **asla** GitHub'a commit edilmemelidir

4. **Veritabanı:**
   - İlk çalıştırmada EF otomatik migration uygular (`Global.asax.cs` → `MigrateDatabaseToLatestVersion`)
   - Connection string: `Web.config` → `TAConn` → `App_Data/TrabzonDB.mdf` (LocalDB)
   - Seed: varsayılan admin `admin@trabzonairlines.com` / `admin123` (`Migrations/Configuration.cs`)

5. **Çalıştırın:**
   - Visual Studio'da `F5` (Start Debugging)
   - IIS Express açılır; URL genelde `http://localhost:<port>/`

6. **Manuel derleme (opsiyonel):**
   ```bash
   # Visual Studio Developer Command Prompt
   msbuild "Trabzon Airlines\Trabzon Airlines\Trabzon Airlines.csproj"
   ```

## 📱 Sayfa Yapısı

### 🏠 Ana Sayfa (Home/Index)
- Hero bölümü ve uçuş arama formu
- Gidiş-Dönüş, Tek Yön, Çoklu Uçuş seçenekleri
- Havalimanı seçim modal'ı
- Yolcu ve kabin sınıfı seçimi
- **Veritabanı entegrasyonu ile gerçek zamanlı uçuş arama**
- **AJAX ile asenkron arama işlemi**
- **Loading durumu göstergesi**
- Öne çıkan uçuş teklifleri (İstanbul, Ankara, İzmir)
- Responsive sidebar navigasyon
- Mobil alt navigasyon

### ✈️ Uçuş Arama Sonuçları (Flights/SearchResults)
- **Gidiş uçuşları listesi** (tarih, saat, fiyat, hava durumu, rötar riski)
- **Dönüş uçuşları listesi** (gidiş-dönüş seçimi için)
- **Uçuş detayları:** Kalkış/varış saatleri, uçuş süresi, sefer numarası
- **Rezervasyon butonu:** Her uçuş için koltuk seçim sayfasına yönlendirme
- **Arama kriterleri özeti:** Seçilen havalimanları ve tarihler
- **Responsive tasarım:** Mobil ve desktop uyumlu

### ℹ️ Hakkımızda (Home/About)
- Şirket bilgileri ve misyon
- Vizyon ve değerler
- İstatistikler (2.5M+ yolcu, 45+ destinasyon, 25 uçak, 15+ yıl)
- Ekip tanıtımı (CEO, CTO, Operations Manager)

### 📞 İletişim (Home/Contact)
- İletişim formu (Ad Soyad, E-posta, Telefon, Konu, Rezervasyon Numarası, Mesaj)
- **KVKK ve Gizlilik Politikası linkleri** - Form altında onay checkbox'ı
- Şirket iletişim bilgileri (Telefon, E-posta, Adres, Çalışma Saatleri)
- Sık sorulan sorular (FAQ)
- Form validasyonu
- **Tam genişlik input alanları** - Modern form tasarımı

### 🆘 Destek (Home/Support)
- Müşteri destek formu (Ad Soyad, E-posta, Konu, PNR, Mesaj)
- İletişim bilgileri kartları
- Sık sorulan konular listesi
- Geçmiş destek talepleri tablosu (giriş yapmış kullanıcılar için)
- Modern kart tasarımı ve gradient efektler
- Responsive tasarım

### 🤖 Trip AI (Home/TripAI)
- Akıllı seyahat planlayıcı
- Form: Şehir, Seyahat Süresi, Bütçe, İlgi Alanı seçimi
- AI chat arayüzü - Mesaj baloncukları
- Örnek seyahat planları
- Modern kart tasarımı ve animasyonlar
- Responsive tasarım

### ✈️ Uçuş Listesi (Flights/Index)
- **Modern kart tasarımı** - Tablo yerine kart görünümü
- Uçuş bilgileri: Sefer numarası, güzergah, saatler, fiyatlar
- **Bilet Satın Al butonu** - Koltuk seçim sayfasına yönlendirme
- Economy ve Business fiyat gösterimi
- Hover efektleri ve animasyonlar
- Responsive grid yapısı

### 📊 Uçuş Durumu (Flights/Status)
- Tüm uçuşların durum bilgileri
- Hava durumu ve rötar riski gösterimi
- Modern tablo tasarımı
- Renkli badge'ler (Yüksek/Orta/Düşük risk)
- Responsive tasarım

### 📋 KVKK Aydınlatma Metni (Home/KVKK)
- 6698 sayılı KVKK kapsamında aydınlatma metni
- Veri sorumlusu bilgileri
- İşlenen kişisel veriler
- İşlenme amaçları ve hukuki sebepler
- Kişisel veri sahibinin hakları
- Güvenlik önlemleri
- İletişim bilgileri
- Modern sayfa tasarımı

### 🔐 Giriş Yap (Home/Login)
- E-posta ve şifre ile giriş
- "Beni hatırla" seçeneği
- Şifremi unuttum özelliği
- Sosyal medya ile giriş (Google, Facebook)
- Kayıt ol sayfasına yönlendirme

### 📝 Kayıt Ol (Home/Signup)
- Kapsamlı kayıt formu
  - Ad, Soyad, E-posta, Telefon
  - TC Kimlik Numarası (opsiyonel)
  - Doğum Tarihi, Cinsiyet
  - Şifre (güç göstergesi ile)
  - Şifre tekrar
  - KVKK ve Kullanım Koşulları onayı
- Sosyal medya ile kayıt (Google, Facebook)
- Üyelik avantajları bilgilendirmesi
- Giriş yap sayfasına yönlendirme
- Otomatik giriş yapma (kayıt sonrası)

### 📦 Paketler (Home/Packages)
- Aktif paketlerin listelenmesi
- Öne çıkan paketlerin öncelikli gösterimi
- Paket filtreleme ve sıralama
- Paket detay sayfasına yönlendirme

### 🎁 Fırsatlar (Home/Deals)
- Aktif fırsatların listelenmesi
- Tarih kontrolü (geçmiş fırsatlar gösterilmez)
- Öne çıkan fırsatların öncelikli gösterimi
- Fırsat detay sayfasına yönlendirme

### ⚙️ Ayarlar (Home/Settings)
- Kullanıcı profil bilgilerini görüntüleme ve düzenleme
- Şifre değiştirme (mevcut şifre doğrulama ile)
- E-posta değiştirme
- Kişisel bilgileri güncelleme
- KVKK ve pazarlama izinlerini yönetme

### 👨‍💼 Admin Paneli (Admin/*)
- **Admin Giriş (Admin/Login)**: Kullanıcı adı ve şifre ile giriş
- **Dashboard (Admin/Dashboard)**: 
  - Toplam paket sayısı
  - Aktif paket sayısı
  - Toplam fırsat sayısı
  - Aktif fırsat sayısı
  - Toplam kullanıcı sayısı
  - Toplam rezervasyon sayısı
- **Paket Yönetimi (Admin/Packages)**:
  - Paket listeleme
  - Yeni paket ekleme
  - Paket düzenleme
  - Paket silme
  - Aktif/Pasif durumu yönetimi
- **Fırsat Yönetimi (Admin/Deals)**:
  - Fırsat listeleme
  - Yeni fırsat ekleme
  - Fırsat düzenleme
  - Fırsat silme
  - Tarih aralığı yönetimi
  - İndirim yüzdesi ayarlama
- **Uçuş Yönetimi (AdminFlights/*)**:
  - Uçuş listeleme
  - Yeni uçuş ekleme
  - Uçuş düzenleme
  - Uçuş silme
  - Aktif/Pasif durumu yönetimi
  - Hava durumu ve rötar riski ayarlama

## 🎨 Tasarım Özellikleri

### Renk Paleti
- **Birincil Renk:** #e4002b (Trabzon Airlines Kırmızısı)
- **İkincil Renk:** #0066cc (Mavi)
- **Vurgu Rengi:** #ff6b35 (Turuncu)
- **Nötr Renkler:** Siyah, gri tonları, beyaz

### Tipografi
- **Ana Font:** System fonts (Apple System, Segoe UI, Roboto)
- **İkonlar:** Font Awesome 6.5.0
- **Responsive:** Mobil-first yaklaşım

### Animasyonlar
- Hover efektleri
- Smooth transitions
- Sidebar açılma/kapanma animasyonları
- Modal açılma/kapanma animasyonları
- Form validasyon feedback'leri

## 🔧 Teknik Detaylar

### Bundle Yapılandırması
- **CSS Bundles:**
  - `~/Content/css` - Ana CSS dosyaları
  - `~/Content/auth` - Auth sayfaları CSS
  - `~/Content/about` - About sayfası CSS
  - `~/Content/contact` - Contact sayfası CSS

- **JavaScript Bundles:**
  - `~/bundles/jquery` - jQuery kütüphanesi
  - `~/bundles/bootstrap` - Bootstrap JS
  - `~/bundles/trabzonairlines` - Trabzon Airlines özel JS dosyaları

### Route Yapılandırması
- `/` - Ana sayfa (Home/Index)
- `/Home/About` - Hakkımızda
- `/Home/Contact` - İletişim
- `/Home/Support` - Müşteri destek
- `/Home/Login` - Giriş yap
- `/Home/Signup` - Kayıt ol
- `/Home/Packages` - Paketler listesi
- `/Home/PackageDetail/{id}` - Paket detay
- `/Home/Deals` - Fırsatlar listesi
- `/Home/DealDetail/{id}` - Fırsat detay
- `/Home/Settings` - Kullanıcı ayarları
- `/Home/Sitemap` - Site haritası
- `/Home/Privacy` - Gizlilik politikası
- `/Home/KVKK` - KVKK aydınlatma metni
- `/Home/TripAI` - Trip AI - Akıllı seyahat planlayıcı
- `/Flights/Index` - Aktif uçuş listesi (Modern kart tasarımı)
- `/Flights/Status` - Uçuş durum sayfası
- `/Flights/Search` - Uçuş arama (POST - JSON API)
- `/Flights/SearchResults` - Uçuş arama sonuçları
- `/Booking/SelectSeat` - Koltuk seçimi (giriş zorunlu)
- `/Booking/MealSelection` - Yemek seçimi
- `/Booking/BookingResult` - Rezervasyon sonucu
- `/CheckIn` - Online check-in ana sayfası (PNR arama)
- `/CheckIn/SearchBooking` - PNR ile rezervasyon arama (POST)
- `/CheckIn/DoCheckIn` - Check-in işlemi (POST)
- `/CheckIn/BoardingPass/{bookingId}` - Biniş kartı görüntüleme
- `/Payment/Index` - Güvenli ödeme sayfası (simüle edilmiş ödeme, giriş zorunlu)
- `/Payment/Pay` - Ödeme işlemi (POST - simüle edilmiş)
- `/Payment/success` - Ödeme başarı sayfası (rezervasyon oluşturma)
- `/Payment/error` - Ödeme hata sayfası
- `/Account/Dashboard` - Kullanıcı dashboard
- `/Account/Account` - Hesap bilgileri
- `/Error/NotFound` - 404 sayfa bulunamadı
- `/Admin/Login` - Admin giriş
- `/Admin/Dashboard` - Admin dashboard
- `/Admin/Packages` - Admin paket yönetimi
- `/Admin/Deals` - Admin fırsat yönetimi
- `/Admin/Settings` - Admin ayarlar
- `/Admin/AdminAcounts` - Admin yönetimi
- `/AdminFlights/Index` - Admin uçuş listesi
- `/AdminFlights/Create` - Yeni uçuş ekleme
- `/AdminFlights/Edit/{id}` - Uçuş düzenleme
- `/AdminFlights/Delete/{id}` - Uçuş silme
- `/AdminBookings/Index` - Admin rezervasyon listesi
- `/AdminBookings/Details/{id}` - Rezervasyon detayı
- `/AdminUsers/Index` - Admin kullanıcı listesi
- `/AdminUsers/Details/{id}` - Kullanıcı detayı
- `/AdminAdmins/Index` - Admin admin listesi
- `/AdminAdmins/Create` - Yeni admin ekleme
- `/AdminAdmins/Edit/{id}` - Admin düzenleme
- `/AdminSupport/Index` - Destek talepleri yönetimi

### Özellikler
- **Havalimanı Yönetimi:** 66+ Türkiye havalimanı (uluslararası ve yerel)
- **Dil Sistemi:** localStorage ile dil tercihi saklama
- **Form Validasyonu:** Client-side ve server-side validasyon
- **Responsive Design:** Mobil, tablet ve desktop uyumluluğu
- **Modal Sistemleri:** Havalimanı seçimi, yolcu seçimi, dil seçimi, uçuş bulunamadı uyarısı
- **Veritabanı:** Entity Framework Code First yaklaşımı
- **Otomatik Migration:** Uygulama başlangıcında otomatik veritabanı güncelleme
- **Güvenlik:** SHA256 şifre hash'leme, Session yönetimi, Anti-forgery token
- **Yetkilendirme:** Admin paneli için özel yetkilendirme attribute'u
- **CRUD İşlemleri:** Paket, fırsat, uçuş, kullanıcı, admin ve rezervasyon yönetimi için tam CRUD desteği
- **Rezervasyon Sistemi:** Paket, fırsat ve uçuş rezervasyonları için model yapısı
- **Ödeme Sistemi:** Simüle edilmiş ödeme sistemi (gerçek ödeme yapılmaz)
- **Koltuk Rezervasyonu:** Uçuş sonrası koltuk seçimi ve rezervasyon sistemi
- **Yemek Sistemi:** Uçuş sırasında yemek siparişi ve rezervasyon-yemek ilişkisi
- **Online Check-In:** PNR ile rezervasyon arama, check-in işlemi ve biniş kartı oluşturma
- **Check-In Validasyonu:** 24 saat kuralı, geçmiş uçuş kontrolü ve tarih validasyonu
- **Biniş Kartı:** Yazdırılabilir biniş kartı ve QR kod desteği
- **Kullanıcı Giriş Kontrolü:** Rezervasyon işlemleri için zorunlu giriş kontrolü
- **ReturnUrl Desteği:** Giriş sonrası önceki sayfaya geri dönüş
- **AJAX Entegrasyonu:** Asenkron uçuş arama işlemi
- **JSON API:** RESTful uçuş arama endpoint'i
- **Uçuş Arama:** Gerçek zamanlı veritabanı sorgulama ile uçuş arama
- **Session Yönetimi:** Rezervasyon bilgilerinin ödeme sayfasına aktarımı
- **Dinamik UI:** Gerçek zamanlı kart bilgisi gösterimi ve para birimi dönüşümü
- **Otomatik Rezervasyon:** Ödeme simülasyonu sonrası otomatik bilet oluşturma
- **Hata Yönetimi:** 404 sayfa bulunamadı ve genel hata sayfaları (ErrorController)
- **KVKK Uyumluluğu:** KVKK aydınlatma metni ve gizlilik politikası sayfaları
- **Destek Sistemi:** Müşteri destek formu ve talep yönetimi (SupportController)
- **Trip AI:** Akıllı seyahat planlayıcı ile örnek seyahat planları
- **Modern CSS Tasarımı:** Tüm sayfalar için tutarlı, modern ve responsive CSS tasarımı
- **Sitemap Güncellemesi:** Admin sayfaları tek kategori altında organize edildi

## 🎯 Gerçekleştirilen Özellikler

### ✅ Tamamlanan Fonksiyonlar
- **ASP.NET MVC Entegrasyonu:** HTML/CSS/JS projesi ASP.NET MVC'ye entegre edildi
- **Responsive Tasarım:** Mobil, tablet ve desktop uyumluluğu
- **Çift Dil Desteği:** Türkçe/İngilizce dinamik dil değiştirme
- **Mobil Navigasyon:** Alt navigasyon çubuğu ve hamburger menü
- **Sidebar Sistemi:** Desktop'ta daraltılabilir sidebar, mobilde drawer
- **Uçuş Arama:** Gelişmiş arama formu ve validasyon
- **Uçuş Arama API:** Veritabanı entegrasyonu ile gerçek zamanlı uçuş arama (FlightsController/Search)
- **Uçuş Sonuçları:** Detaylı uçuş sonuçları sayfası (SearchResults)
- **Uçuş Listesi:** Modern kart tasarımı ile aktif uçuş listesi
- **Uçuş Durumu:** Hava durumu ve rötar riski ile uçuş durum sayfası
- **Popup Modal:** Uçuş bulunamadığında kullanıcıya bilgilendirme modal'ı
- **Kullanıcı Yönetimi:** Login, Signup, Settings, Dashboard ve Account sayfaları
- **Form Validasyonu:** Client-side ve server-side validasyon
- **Modal Sistemleri:** Havalimanı, yolcu, dil seçim modalları
- **Bundle & Minification:** CSS/JS optimizasyonu
- **Veritabanı Entegrasyonu:** Entity Framework ile LocalDB kullanımı
- **Admin Paneli:** Tam özellikli admin yönetim sistemi
- **Paket Yönetimi:** CRUD işlemleri ile paket yönetimi
- **Fırsat Yönetimi:** CRUD işlemleri ile fırsat yönetimi
- **Uçuş Yönetimi:** CRUD işlemleri ile uçuş yönetimi
- **Kullanıcı Yönetimi:** Admin panelinde kullanıcı listeleme ve detay görüntüleme
- **Admin Yönetimi:** Admin kullanıcıları için CRUD işlemleri
- **Destek Yönetimi:** Müşteri destek taleplerini yönetme
- **Güvenlik:** SHA256 şifre hash'leme, Session yönetimi
- **Yetkilendirme:** Admin paneli için özel yetkilendirme sistemi
- **Ödeme Sistemi:** Simüle edilmiş ödeme sistemi (gerçek ödeme yapılmaz)
- **Koltuk Seçimi:** Uçuş sonrası koltuk seçimi ve rezervasyon sistemi
- **Yemek Seçimi:** Uçuş sırasında yemek siparişi ve rezervasyon-yemek ilişkisi
- **Online Check-In:** PNR ile rezervasyon arama, check-in işlemi ve biniş kartı oluşturma
- **Check-In Validasyonu:** 24 saat kuralı, geçmiş uçuş kontrolü ve tarih validasyonu
- **Biniş Kartı:** Yazdırılabilir biniş kartı ve QR kod desteği
- **Kullanıcı Giriş Kontrolü:** Rezervasyon işlemleri için zorunlu giriş kontrolü
- **ReturnUrl Desteği:** Giriş sonrası önceki sayfaya geri dönüş
- **Hata Yönetimi:** 404 sayfa bulunamadı ve genel hata sayfaları
- **Veritabanı Bağlantı Yönetimi:** Güvenli veritabanı bağlantı kontrolü ve hata yakalama
- **KVKK Uyumluluğu:** KVKK aydınlatma metni ve gizlilik politikası sayfaları
- **Destek Sistemi:** Müşteri destek formu ve geçmiş talep görüntüleme
- **Trip AI:** Akıllı seyahat planlayıcı ile örnek seyahat planları
- **Modern CSS Tasarımı:** Tüm sayfalar için tutarlı, modern ve responsive CSS tasarımı

### 🔧 Teknik Özellikler
- ASP.NET MVC 5 (.NET Framework 4.7.2)
- Razor View Engine
- Bundle & Minification
- Entity Framework 6.5.1 (Code First)
- SQL Server LocalDB
- Modern CSS3 (Grid, Flexbox, Custom Properties)
- Vanilla JavaScript (ES6+)
- Font Awesome 6.5.0 ikon kütüphanesi
- jQuery 3.7.0
- Bootstrap 5
- SHA256 şifre hash'leme
- Session yönetimi
- Anti-forgery token koruması
- SEO optimizasyonu (Meta tags, Open Graph)
- Cross-browser uyumluluk

## 📝 Notlar

### Önemli Dosyalar
- `BundleConfig.cs` - CSS/JS bundle yapılandırması
- `_Layout.cshtml` - Master layout (header, sidebar, footer, modals - uçuş bulunamadı modal'ı dahil)
- `_AdminLayout.cshtml` - Admin paneli layout
- `HomeController.cs` - Ana controller ve action'lar (Index, About, Contact, Support, Login, Signup, TripAI, Privacy, KVKK, Sitemap)
- `AdminController.cs` - Admin paneli controller'ı (Dashboard, Settings, AdminAcounts)
- `FlightsController.cs` - Uçuş controller (Index, Status, Search API, SearchResults)
- `AdminFlightsController.cs` - Admin uçuş yönetimi controller'ı (CRUD işlemleri)
- `BookingController.cs` - Rezervasyon controller'ı (SelectSeat - koltuk seçimi, MealSelection - yemek seçimi, BookingResult, giriş kontrolü)
- `CheckInController.cs` - Check-in controller'ı (Index - PNR arama, SearchBooking, DoCheckIn, BoardingPass - biniş kartı)
- `PaymentController.cs` - Ödeme controller'ı (Index, Pay, success, error - simüle edilmiş ödeme)
- `AccountController.cs` - Hesap yönetimi controller'ı (Dashboard, Account)
- `SupportController.cs` - Destek controller'ı (Support form POST)
- `ErrorController.cs` - Hata yönetimi controller'ı (NotFound, Index)
- `AdminBookingsController.cs` - Admin rezervasyon yönetimi controller'ı
- `AdminUsersController.cs` - Admin kullanıcı yönetimi controller'ı
- `AdminAdminsController.cs` - Admin admin yönetimi controller'ı
- `AdminSupport.cs` - Admin destek talepleri controller'ı
- `TAContext.cs` - Entity Framework veritabanı bağlamı (Flights, Seats, FlightSeats, Bookings, Users, Admins, SupportRequests, Meals, BookingMeals)
- `AuthorizeAdminAttribute.cs` - Admin yetkilendirme attribute'u
- `Web.config` - Web uygulaması yapılandırması (Connection String: TAConn, Custom Errors)
- `Global.asax.cs` - Otomatik migration yapılandırması ve hata yönetimi
- `RouteConfig.cs` - Route yapılandırması (404 route dahil)
- `Migrations/Configuration.cs` - Migration yapılandırması (AutomaticMigrationsEnabled = true)
- `Models/` - Veritabanı modelleri (User, Admin, Flight, Seat, FlightSeat, Booking, Package, Deal, SupportRequest, vb.)
- `js/flight-search.js` - Uçuş arama JavaScript fonksiyonları (AJAX entegrasyonu dahil)
- `js/tripai.js` - Trip AI JavaScript fonksiyonları
- `Content/css/support.css` - Destek sayfası CSS
- `Content/css/trip.css` - Trip AI sayfası CSS
- `Content/css/flight-list.css` - Uçuş listesi CSS
- `Content/css/flight-status.css` - Uçuş durumu CSS

### Veritabanı Modelleri
- **User:** Kullanıcı bilgileri (Ad, Soyad, E-posta, Şifre, TC, Doğum Tarihi, Cinsiyet, Telefon, vb.)
- **Admin:** Admin kullanıcı bilgileri (Kullanıcı adı, Şifre, Ad, Soyad, E-posta, Super Admin durumu, Aktif durumu)
- **Flight:** Uçuş bilgileri (Sefer No, Kalkış/Varış havalimanı kodları, Kalkış/Varış zamanları, Economy/Business fiyatları, Aktif durumu, Hava durumu, Rötar riski)
- **Seat:** Koltuk bilgileri (Koltuk numarası, Kabin sınıfı)
- **FlightSeat:** Uçuş-Koltuk ilişki tablosu (Uçuş ID, Koltuk ID, Rezerve durumu)
- **Booking:** Rezervasyon bilgileri (Uçuş ID, Koltuk ID, Yolcu bilgileri, PNR, Rezervasyon tarihi, Fiyat, CheckedIn, CheckInTime) - Ödeme simülasyonu sonrası otomatik oluşturulur
- **Meal:** Yemek bilgileri (Yemek adı, Açıklama, Fiyat, Kategori)
- **BookingMeal:** Rezervasyon-Yemek ilişki tablosu (Rezervasyon ID, Yemek ID, Miktar)
- **SupportRequest:** Destek talepleri (Ad Soyad, E-posta, Konu, PNR, Mesaj, Durum, Kullanıcı ID, Oluşturulma tarihi)
- **Package:** Paket bilgileri (Başlık, Açıklama, Fiyat, İndirim, Kategori, vb.)
- **Deal:** Fırsat bilgileri (Başlık, Açıklama, İndirim, Tarih aralığı, Kampanya kodu, vb.)
- **PackageReservation:** Paket rezervasyonları
- **DealReservation:** Fırsat rezervasyonları

### Assets Klasörü
Assets klasörü (`Trabzon Airlines/Trabzon Airlines/assets/`) proje içinde bulunmalıdır:
- `assets/images/destinations/` - Destinasyon resimleri
- `assets/images/team/` - Ekip üyesi resimleri
- `assets/logo/` - Logo dosyaları
- `assets/fonts/` - Özel font dosyaları
- `assets/icons/` - SVG ikon dosyaları

### Veritabanı Kurulumu
1. Proje ilk çalıştırıldığında Entity Framework otomatik olarak veritabanını oluşturur / migrate eder
2. **Otomatik Migration:** `Global.asax.cs` → `MigrateDatabaseToLatestVersion`
3. **Migration Yapılandırması:** `Migrations/Configuration.cs`
   - `AutomaticMigrationsEnabled = true`
   - `AutomaticMigrationDataLossAllowed = true` (development)
4. Veritabanı: `TrabzonDB` (LocalDB) — dosya: `App_Data/TrabzonDB.mdf` (gitignore)
5. Connection String: `Web.config` → `TAConn`
6. **Seed admin:** `admin@trabzonairlines.com` / `admin123` (SHA256 hash ile kaydedilir)
7. **Secret'lar:** API key ve SMTP şifresi User Secrets'ta tutulur (`secrets.example.xml` şablonuna bakın)
## 🌐 GitHub Repository

**Proje Linki:** [https://github.com/OnurAkyuz61/SEN0421-SoftwareDevelopment-Project](https://github.com/OnurAkyuz61/SEN0421-SoftwareDevelopment-Project)

## 📝 Lisans

Bu proje eğitim amaçlı geliştirilmiştir ve İstanbul Kültür Üniversitesi **SEN0421 - Software Development with Standards** dersi kapsamındadır.

## 🎓 Proje Teslim Bilgileri

**Proje Durumu:** 🚧 **DEVAM EDİYOR** (henüz tamamlanmadı / teslim edilmedi)  
**Ders:** SEN0421 - Software Development with Standards  
**Ekip:** Emirhan Mollahüseyinoğlu, Onur Akyüz, Rana Neslihan Kurt, Zeynep Avcı

### 📊 Proje İstatistikleri (güncel)
- **View sayısı:** ~51
- **Controller:** 17
- **Model:** 17
- **Services:** 3
- **Dil desteği:** Türkçe / İngilizce

### 🏆 Mevcut Durum
- ✅ Temel rezervasyon, check-in, ödeme simülasyonu ve admin paneli çalışıyor
- ✅ Secret yönetimi User Secrets'a taşındı
- 🚧 Geliştirme ve standardizasyon çalışmaları devam ediyor
- ⏳ Final teslim henüz yapılmadı

## 🙏 Teşekkürler

- İstanbul Kültür Üniversitesi Bilgisayar Mühendisliği Bölümü
- SEN0421 - Software Development with Standards dersi öğretim görevlileri
- Ekip üyeleri

---

## 📞 İletişim

Proje hakkında sorularınız için:
- **GitHub:** [https://github.com/OnurAkyuz61/SEN0421-SoftwareDevelopment-Project](https://github.com/OnurAkyuz61/SEN0421-SoftwareDevelopment-Project)

---

**© 2025–2027 Trabzon Airlines | İstanbul Kültür Üniversitesi — SEN0421**

*Bu proje eğitim amaçlı geliştirilmiştir ve gerçek bir havayolu şirketi değildir.*

---

## 🎯 Son Notlar

Bu proje ASP.NET MVC ile geliştirilen bir havayolu rezervasyon sistemidir. **Henüz tamamlanmamıştır**; özellikler ve kod kalitesi üzerinde çalışmaya devam edilmektedir.

**🚧 Proje devam ediyor — final sürüm değildir.**
