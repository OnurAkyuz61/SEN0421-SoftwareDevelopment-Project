# 🛫 Trabzon Airlines - Havayolu Rezervasyon Sistemi

## 📋 Proje Bilgileri

**Üniversite:** İstanbul Kültür Üniversitesi  
**Ders:** COM5005 - Web Programming  
**Dönem:** 2025-2026 Güz Dönemi  
**Grup:** Grup 20  
**Proje Konusu:** Airline Reservation System (Havayolu Rezervasyon Sistemi)

## 👥 Ekip Üyeleri

| Öğrenci No | Ad Soyad |
|------------|----------|
| 21000005226 | Emirhan Mollahüseyinoğlu |
| 21000005934 | Cem Çalışkan |
| 21000005689 | Elif Başak Yılmaz |
| 22000005590 | Onur Akyüz |
| 23000007786 | Eren Özduran |

## 🎯 Proje Açıklaması

Trabzon Airlines, modern web teknolojileri kullanılarak geliştirilmiş kapsamlı bir havayolu rezervasyon sistemidir. Proje, kullanıcıların uçuş arama, rezervasyon yapma, hesap yönetimi ve çeşitli havayolu hizmetlerine erişim sağlayabileceği responsive bir web uygulamasıdır.

### ✨ Özellikler

- 🔍 **Uçuş Arama ve Rezervasyon**
  - Gelişmiş arama filtreleri
  - Gerçek zamanlı fiyat karşılaştırması
  - Esnek tarih seçenekleri

- 👤 **Kullanıcı Yönetimi**
  - Güvenli kayıt ve giriş sistemi
  - Profil yönetimi
  - Rezervasyon geçmişi

- 🌍 **Çok Dilli Destek**
  - Türkçe ve İngilizce dil seçenekleri
  - Dinamik dil değiştirme
  - Yerelleştirme desteği

- 📱 **Responsive Tasarım**
  - Mobil uyumlu arayüz
  - Tablet ve desktop optimizasyonu
  - Modern UI/UX tasarımı

- 💳 **Ödeme Sistemi**
  - Güvenli ödeme altyapısı
  - Çoklu para birimi desteği
  - Kredi kartı ve dijital cüzdan entegrasyonu

## 🛠️ Kullanılan Teknolojiler

### Frontend
- **HTML5** - Semantik yapı ve erişilebilirlik
- **CSS3** - Modern styling ve animasyonlar
- **Bootstrap 5** - Responsive framework ve UI bileşenleri
- **JavaScript (ES6+)** - İnteraktif özellikler ve DOM manipülasyonu
- **Font Awesome** - İkon kütüphanesi
- **Responsive Design** - Mobil uyumlu tasarım

### Backend (Planlanan)
- **Node.js** - Server-side JavaScript
- **Express.js** - Web framework
- **MongoDB** - NoSQL veritabanı
- **JWT** - Kimlik doğrulama

### Araçlar ve Platformlar
- **Git & GitHub** - Versiyon kontrolü
- **VS Code** - Geliştirme ortamı
- **Browser Developer Tools** - Debug ve test

## 📁 Proje Yapısı

```
Trabzon Airlines/
├── 📄 index.html              # Ana sayfa
├── 📁 pages/                  # Diğer sayfalar
│   ├── about.html             # Hakkımızda
│   ├── contact.html           # İletişim
│   ├── login.html             # Giriş yap
│   └── signup.html            # Kayıt ol
├── 📁 css/                    # Stil dosyaları
│   └── style.css              # Ana CSS dosyası
├── 📁 js/                     # JavaScript dosyaları
│   └── script.js              # Ana JS dosyası
├── 📁 assets/                 # Medya dosyaları
│   └── icons/                 # İkonlar
├── 📁 images/                 # Görseller
└── 📄 README.md               # Proje dokümantasyonu
```

## 🚀 Kurulum ve Çalıştırma

### Gereksinimler
- Modern web tarayıcısı (Chrome, Firefox, Safari, Edge)
- Yerel web sunucusu (opsiyonel)

### Kurulum Adımları

1. **Projeyi klonlayın:**
   ```bash
   git clone https://github.com/username/trabzon-airlines.git
   cd trabzon-airlines
   ```

2. **Yerel sunucu başlatın (opsiyonel):**
   ```bash
   # Python ile
   python -m http.server 3000
   
   # Node.js ile
   npx serve .
   
   # VS Code Live Server eklentisi ile
   # Sağ tık > "Open with Live Server"
   ```

3. **Tarayıcıda açın:**
   ```
   http://localhost:3000
   ```

## 📱 Sayfa Yapısı

### 🏠 Ana Sayfa (index.html)
- Hero bölümü ve uçuş arama formu
- Öne çıkan uçuş teklifleri
- Responsive sidebar navigasyon
- Mobil alt navigasyon

### ℹ️ Hakkımızda (about.html)
- Şirket bilgileri ve misyon
- Ekip tanıtımı
- İstatistikler ve başarılar

### 📞 İletişim (contact.html)
- İletişim formu
- Şirket iletişim bilgileri
- Sık sorulan sorular (FAQ)
- Harita entegrasyonu (planlanan)

### 🔐 Kimlik Doğrulama
- **Giriş Yap (login.html):** Kullanıcı girişi
- **Kayıt Ol (signup.html):** Yeni kullanıcı kaydı

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
- Form validasyon feedback'leri

## 🌟 Öne Çıkan Özellikler

### Çift Dilli Sistem
- Türkçe ve İngilizce dil desteği
- `data-tr` ve `data-en` attributeları ile dinamik çeviri
- Kullanıcı tercihlerinin localStorage'da saklanması

### Responsive Sidebar
- Desktop'ta daraltılabilir sidebar
- Mobil'de drawer tarzı açılım
- Smooth animasyonlar ve geçişler

### Modern Form Tasarımı
- Gelişmiş form validasyonu
- Şifre gücü göstergesi
- Sosyal medya login seçenekleri

### Accessibility (Erişilebilirlik)
- ARIA labels ve semantic HTML
- Keyboard navigation desteği
- Screen reader uyumluluğu

## 📈 Geliştirme Süreci

### Faz 1: Planlama ve Tasarım ✅
- Proje gereksinimlerinin belirlenmesi
- Wireframe ve mockup tasarımları
- Teknoloji stack seçimi

### Faz 2: Frontend Geliştirme ✅
- HTML yapısının oluşturulması
- CSS ile responsive tasarım
- JavaScript ile interaktivite

### Faz 3: Backend Entegrasyonu (Devam Ediyor)
- API tasarımı ve geliştirme
- Veritabanı şeması
- Güvenlik implementasyonu

### Faz 4: Test ve Optimizasyon (Planlanan)
- Cross-browser testing
- Performance optimizasyonu
- Bug fixes ve iyileştirmeler

## 🧪 Test Senaryoları

### Fonksiyonel Testler
- [ ] Uçuş arama ve filtreleme
- [ ] Kullanıcı kayıt ve giriş
- [ ] Dil değiştirme
- [ ] Form validasyonları
- [ ] Responsive tasarım

### Uyumluluk Testleri
- [ ] Chrome, Firefox, Safari, Edge
- [ ] iOS Safari, Android Chrome
- [ ] Tablet ve desktop çözünürlükleri

## 🔒 Güvenlik Önlemleri

- XSS koruması
- CSRF token kullanımı
- Input sanitization
- Güvenli authentication
- HTTPS protokolü (production)

## 📊 Performance Metrikleri

- Sayfa yükleme süresi: < 3 saniye
- First Contentful Paint: < 1.5 saniye
- Lighthouse Score: > 90
- Mobile-friendly test: Passed

## 🤝 Katkıda Bulunma

1. Fork edin
2. Feature branch oluşturun (`git checkout -b feature/amazing-feature`)
3. Değişikliklerinizi commit edin (`git commit -m 'Add amazing feature'`)
4. Branch'i push edin (`git push origin feature/amazing-feature`)
5. Pull Request oluşturun

## 📝 Lisans

Bu proje eğitim amaçlı geliştirilmiştir ve İstanbul Kültür Üniversitesi COM5005 dersi kapsamındadır.


## 🙏 Teşekkürler

- İstanbul Kültür Üniversitesi Bilgisayar Mühendisliği Bölümü
- COM5005 Web Programming dersi öğretim görevlileri
- Tüm ekip üyeleri ve katkıda bulunanlar

---

**© 2025 Trabzon Airlines - Grup 20 | İstanbul Kültür Üniversitesi**

*Bu proje eğitim amaçlı geliştirilmiştir ve gerçek bir havayolu şirketi değildir.*
