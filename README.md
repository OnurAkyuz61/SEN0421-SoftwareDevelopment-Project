# 🛫 Trabzon Airlines - Havayolu Rezervasyon Sistemi

## 📋 Proje Bilgileri

**Üniversite:** İstanbul Kültür Üniversitesi  
**Ders:** COM5005 - Web Programming  
**Dönem:** 2025-2026 Güz Dönemi  
**Grup:** Grup 20  
**Proje Konusu:** Airline Reservation System (Havayolu Rezervasyon Sistemi)  
**Teknoloji:** ASP.NET MVC (.NET Framework)

## 👥 Ekip Üyeleri

| Öğrenci No | Ad Soyad |
|------------|----------|
| 21000005226 | Emirhan Mollahüseyinoğlu |
| 21000005934 | Cem Çalışkan |
| 21000005689 | Elif Başak Yılmaz |
| 22000005590 | Onur Akyüz |
| 23000007786 | Eren Özduran |

## 🎯 Proje Açıklaması

Trabzon Airlines, ASP.NET MVC (.NET Framework) kullanılarak geliştirilmiş kapsamlı bir havayolu rezervasyon sistemidir. Proje, kullanıcıların uçuş arama, rezervasyon yapma, hesap yönetimi ve çeşitli havayolu hizmetlerine erişim sağlayabileceği responsive bir web uygulamasıdır.

### ✨ Özellikler

- 🔍 **Uçuş Arama ve Rezervasyon**
  - Gelişmiş arama filtreleri (Gidiş-Dönüş, Tek Yön, Çoklu Uçuş)
  - Havalimanı seçim modal'ı
  - Yolcu ve kabin sınıfı seçimi
  - Tarih validasyonu
  - Promosyon kodu desteği

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

- 📦 **Paket ve Fırsat Yönetimi**
  - Paket listeleme ve detay sayfaları
  - Fırsat listeleme ve detay sayfaları
  - Paket rezervasyon sistemi
  - Fırsat rezervasyon sistemi
  - Aktif/Pasif paket yönetimi
  - Öne çıkan paket/fırsat işaretleme

- 👨‍💼 **Admin Paneli** ✅
  - Admin giriş sistemi (yetkilendirme ile)
  - Dashboard (istatistikler ve özet bilgiler)
  - Paket yönetimi (Ekleme, Düzenleme, Silme, Listeleme)
  - Fırsat yönetimi (Ekleme, Düzenleme, Silme, Listeleme)
  - Kullanıcı yönetimi
  - Rezervasyon takibi
  - Super Admin yetkilendirme sistemi

- 📄 **Sayfalar**
  - Ana Sayfa (Index) - Uçuş arama ve öne çıkan teklifler
  - Hakkımızda (About) - Şirket bilgileri, misyon, vizyon, ekip
  - İletişim (Contact) - İletişim formu ve FAQ
  - Giriş Yap (Login) - Kullanıcı girişi
  - Kayıt Ol (Signup) - Yeni kullanıcı kaydı
  - Paketler (Packages) - Tüm paketleri listeleme
  - Paket Detay (PackageDetail) - Paket detay sayfası
  - Fırsatlar (Deals) - Tüm fırsatları listeleme
  - Fırsat Detay (DealDetail) - Fırsat detay sayfası
  - Ayarlar (Settings) - Kullanıcı profil ve ayar yönetimi
  - Site Haritası (Sitemap) - Site yapısı
  - Gizlilik Politikası (Privacy) - KVKK ve gizlilik bilgileri

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

### Frontend
- **HTML5** - Semantik yapı ve erişilebilirlik
- **CSS3** - Modern styling, animasyonlar ve responsive tasarım
  - Custom CSS dosyaları (style.css, about.css, auth.css, contact.css)
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

## 📁 Proje Yapısı

```
web-prog-proj/
├── Trabzon Airlines/
│   └── Trabzon Airlines/          # ASP.NET MVC Projesi
│       ├── App_Start/
│       │   ├── BundleConfig.cs   # CSS/JS bundle yapılandırması
│       │   ├── FilterConfig.cs   # Filter yapılandırması
│       │   └── RouteConfig.cs     # Route yapılandırması
│       ├── Controllers/
│       │   ├── HomeController.cs   # Ana controller (Index, About, Contact, Login, Signup, Packages, Deals, Settings)
│       │   └── AdminController.cs  # Admin controller (Dashboard, Packages, Deals yönetimi)
│       ├── Models/
│       │   ├── User.cs             # Kullanıcı modeli
│       │   ├── Admin.cs            # Admin modeli
│       │   ├── Package.cs          # Paket modeli
│       │   ├── Deal.cs             # Fırsat modeli
│       │   ├── PackageReservation.cs # Paket rezervasyon modeli
│       │   ├── DealReservation.cs  # Fırsat rezervasyon modeli
│       │   └── TrabzonAirlinesDbContext.cs # Entity Framework DbContext
│       ├── Attributes/
│       │   └── AuthorizeAdminAttribute.cs # Admin yetkilendirme attribute'u
│       ├── Views/
│       │   ├── Home/
│       │   │   ├── Index.cshtml      # Ana sayfa - Uçuş arama
│       │   │   ├── About.cshtml      # Hakkımızda
│       │   │   ├── Contact.cshtml   # İletişim
│       │   │   ├── Login.cshtml      # Giriş yap
│       │   │   ├── Signup.cshtml     # Kayıt ol
│       │   │   ├── Packages.cshtml   # Paketler listesi
│       │   │   ├── PackageDetail.cshtml # Paket detay
│       │   │   ├── Deals.cshtml      # Fırsatlar listesi
│       │   │   ├── DealDetail.cshtml # Fırsat detay
│       │   │   ├── Settings.cshtml   # Kullanıcı ayarları
│       │   │   ├── Sitemap.cshtml    # Site haritası
│       │   │   └── Privacy.cshtml    # Gizlilik politikası
│       │   ├── Admin/
│       │   │   ├── Login.cshtml      # Admin giriş
│       │   │   ├── Dashboard.cshtml  # Admin dashboard
│       │   │   ├── Packages.cshtml   # Paket yönetimi listesi
│       │   │   ├── AddPackage.cshtml  # Paket ekleme
│       │   │   ├── EditPackage.cshtml # Paket düzenleme
│       │   │   ├── Deals.cshtml      # Fırsat yönetimi listesi
│       │   │   ├── AddDeal.cshtml    # Fırsat ekleme
│       │   │   └── EditDeal.cshtml   # Fırsat düzenleme
│       │   └── Shared/
│       │       └── _Layout.cshtml # Master layout
│       ├── Content/
│       │   ├── css/
│       │   │   ├── style.css      # Ana CSS
│       │   │   ├── about.css       # Hakkımızda CSS
│       │   │   ├── auth.css       # Auth sayfaları CSS
│       │   │   └── contact.css   # İletişim CSS
│       │   └── bootstrap.css      # Bootstrap CSS
│       ├── Scripts/
│       │   ├── airports.js        # Havalimanı yönetimi
│       │   ├── language.js        # Dil sistemi
│       │   ├── sidebar.js         # Sidebar kontrolü
│       │   ├── flight-search.js   # Uçuş arama
│       │   ├── forms.js           # Form işlemleri
│       │   ├── alerts.js          # Uyarı mesajları
│       │   ├── multi-city.js      # Çoklu uçuş
│       │   ├── trip-type.js       # Seyahat türü
│       │   ├── main.js            # Ana initialization
│       │   ├── auth.js            # Kimlik doğrulama
│       │   ├── jquery-3.7.0.js    # jQuery
│       │   └── bootstrap.js       # Bootstrap JS
│       ├── assets/
│       │   ├── images/
│       │   │   ├── destinations/  # Destinasyon resimleri
│       │   │   └── team/         # Ekip üyesi resimleri
│       │   └── logo/
│       │       └── logo.png      # Logo
│       ├── App_Data/              # Veritabanı dosyaları (LocalDB)
│       ├── Web.config             # Web yapılandırması (Connection String dahil)
│       ├── Global.asax            # Global uygulama dosyası
│       └── packages.config        # NuGet paketleri
├── css/                            # Orijinal CSS dosyaları (referans)
├── js/                             # Orijinal JS dosyaları (referans)
├── assets/                         # Orijinal assets (referans)
├── pages/                          # Orijinal HTML sayfaları (referans)
├── index.html                      # Orijinal ana sayfa (referans)
└── README.md                       # Proje dokümantasyonu
```

## 🚀 Kurulum ve Çalıştırma

### Gereksinimler
- **Visual Studio 2022** veya **Visual Studio 2019**
- **.NET Framework 4.7.2** veya üzeri
- **SQL Server LocalDB** (Visual Studio ile birlikte gelir veya ayrı yüklenebilir)
- **IIS Express** (Visual Studio ile birlikte gelir)
- Modern web tarayıcısı (Chrome, Firefox, Safari, Edge)

### Kurulum Adımları

1. **Projeyi klonlayın:**
   ```bash
   git clone https://github.com/OnurAkyuz61/COM5005-WebProgramming-Project.git
   cd COM5005-WebProgramming-Project
   ```

2. **Visual Studio'da açın:**
   - `Trabzon Airlines/Trabzon Airlines.sln` dosyasını Visual Studio ile açın
   - NuGet paketlerinin otomatik olarak restore edilmesini bekleyin

3. **Veritabanını oluşturun:**
   - Proje ilk çalıştırıldığında Entity Framework otomatik olarak veritabanını oluşturacaktır
   - Veritabanı: `TrabzonAirlinesDb` (LocalDB)
   - Connection String: `Web.config` dosyasında tanımlıdır
   - İlk admin kullanıcısı oluşturmak için veritabanına manuel olarak eklenmelidir

4. **Projeyi çalıştırın:**
   - Visual Studio'da `F5` tuşuna basın veya "Start Debugging" butonuna tıklayın
   - IIS Express otomatik olarak başlatılacak ve tarayıcı açılacak
   - Varsayılan URL: `http://localhost:port/`

4. **Manuel çalıştırma (opsiyonel):**
   ```bash
   # Visual Studio Developer Command Prompt'ta
   msbuild "Trabzon Airlines\Trabzon Airlines\Trabzon Airlines.csproj"
   ```

## 📱 Sayfa Yapısı

### 🏠 Ana Sayfa (Home/Index)
- Hero bölümü ve uçuş arama formu
- Gidiş-Dönüş, Tek Yön, Çoklu Uçuş seçenekleri
- Havalimanı seçim modal'ı
- Yolcu ve kabin sınıfı seçimi
- Öne çıkan uçuş teklifleri (İstanbul, Ankara, İzmir)
- Responsive sidebar navigasyon
- Mobil alt navigasyon

### ℹ️ Hakkımızda (Home/About)
- Şirket bilgileri ve misyon
- Vizyon ve değerler
- İstatistikler (2.5M+ yolcu, 45+ destinasyon, 25 uçak, 15+ yıl)
- Ekip tanıtımı (CEO, CTO, Operations Manager)

### 📞 İletişim (Home/Contact)
- İletişim formu (Ad Soyad, E-posta, Telefon, Konu, Mesaj)
- Şirket iletişim bilgileri (Telefon, E-posta, Adres, Çalışma Saatleri)
- Sık sorulan sorular (FAQ)
- Form validasyonu

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
- `/Home/Login` - Giriş yap
- `/Home/Signup` - Kayıt ol
- `/Home/Packages` - Paketler listesi
- `/Home/PackageDetail/{id}` - Paket detay
- `/Home/Deals` - Fırsatlar listesi
- `/Home/DealDetail/{id}` - Fırsat detay
- `/Home/Settings` - Kullanıcı ayarları
- `/Home/Sitemap` - Site haritası
- `/Home/Privacy` - Gizlilik politikası
- `/Admin/Login` - Admin giriş
- `/Admin/Dashboard` - Admin dashboard
- `/Admin/Packages` - Admin paket yönetimi
- `/Admin/Deals` - Admin fırsat yönetimi

### Özellikler
- **Havalimanı Yönetimi:** 66+ Türkiye havalimanı (uluslararası ve yerel)
- **Dil Sistemi:** localStorage ile dil tercihi saklama
- **Form Validasyonu:** Client-side ve server-side validasyon
- **Responsive Design:** Mobil, tablet ve desktop uyumluluğu
- **Modal Sistemleri:** Havalimanı seçimi, yolcu seçimi, dil seçimi
- **Veritabanı:** Entity Framework Code First yaklaşımı
- **Güvenlik:** SHA256 şifre hash'leme, Session yönetimi, Anti-forgery token
- **Yetkilendirme:** Admin paneli için özel yetkilendirme attribute'u
- **CRUD İşlemleri:** Paket ve fırsat yönetimi için tam CRUD desteği
- **Rezervasyon Sistemi:** Paket ve fırsat rezervasyonları için model yapısı

## 🎯 Gerçekleştirilen Özellikler

### ✅ Tamamlanan Fonksiyonlar
- **ASP.NET MVC Entegrasyonu:** HTML/CSS/JS projesi ASP.NET MVC'ye entegre edildi
- **Responsive Tasarım:** Mobil, tablet ve desktop uyumluluğu
- **Çift Dil Desteği:** Türkçe/İngilizce dinamik dil değiştirme
- **Mobil Navigasyon:** Alt navigasyon çubuğu ve hamburger menü
- **Sidebar Sistemi:** Desktop'ta daraltılabilir sidebar, mobilde drawer
- **Uçuş Arama:** Gelişmiş arama formu ve validasyon
- **Kullanıcı Yönetimi:** Login, Signup ve Settings sayfaları
- **Form Validasyonu:** Client-side ve server-side validasyon
- **Modal Sistemleri:** Havalimanı, yolcu, dil seçim modalları
- **Bundle & Minification:** CSS/JS optimizasyonu
- **Veritabanı Entegrasyonu:** Entity Framework ile LocalDB kullanımı
- **Admin Paneli:** Tam özellikli admin yönetim sistemi
- **Paket Yönetimi:** CRUD işlemleri ile paket yönetimi
- **Fırsat Yönetimi:** CRUD işlemleri ile fırsat yönetimi
- **Güvenlik:** SHA256 şifre hash'leme, Session yönetimi
- **Yetkilendirme:** Admin paneli için özel yetkilendirme sistemi

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
- `_Layout.cshtml` - Master layout (header, sidebar, footer, modals)
- `HomeController.cs` - Ana controller ve action'lar
- `AdminController.cs` - Admin paneli controller'ı
- `TrabzonAirlinesDbContext.cs` - Entity Framework veritabanı bağlamı
- `AuthorizeAdminAttribute.cs` - Admin yetkilendirme attribute'u
- `Web.config` - Web uygulaması yapılandırması (Connection String dahil)
- `Models/` - Veritabanı modelleri (User, Admin, Package, Deal, vb.)

### Veritabanı Modelleri
- **User:** Kullanıcı bilgileri (Ad, Soyad, E-posta, Şifre, TC, vb.)
- **Admin:** Admin kullanıcı bilgileri (Kullanıcı adı, Şifre, Super Admin durumu)
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
1. Proje ilk çalıştırıldığında Entity Framework otomatik olarak veritabanını oluşturur
2. Veritabanı: `TrabzonAirlinesDb` (LocalDB)
3. Connection String: `Web.config` dosyasında `TrabzonAirlinesDb` olarak tanımlıdır
4. İlk admin kullanıcısı oluşturmak için:
   - Veritabanına manuel olarak admin eklenmelidir
   - Şifre SHA256 ile hash'lenmelidir
   - `IsActive = true` ve `IsSuperAdmin = true` olarak ayarlanmalıdır

## 🌐 GitHub Repository

**Proje Linki:** [https://github.com/OnurAkyuz61/COM5005-WebProgramming-Project](https://github.com/OnurAkyuz61/COM5005-WebProgramming-Project)

## 📝 Lisans

Bu proje eğitim amaçlı geliştirilmiştir ve İstanbul Kültür Üniversitesi COM5005 dersi kapsamındadır.

## 🙏 Teşekkürler

- İstanbul Kültür Üniversitesi Bilgisayar Mühendisliği Bölümü
- COM5005 Web Programming dersi öğretim görevlileri
- Tüm ekip üyeleri ve katkıda bulunanlar

---

**© 2025 Trabzon Airlines - Grup 20 | İstanbul Kültür Üniversitesi**

*Bu proje eğitim amaçlı geliştirilmiştir ve gerçek bir havayolu şirketi değildir.*
