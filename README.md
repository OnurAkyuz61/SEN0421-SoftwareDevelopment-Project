# ServiceDesk – ISO 9001 IT Servis Masası (SDS-07)

## Proje Bilgileri

**Üniversite:** İstanbul Kültür Üniversitesi  
**Ders:** SEN0421 - Software Development with Standards  
**Dönem:** 2026-2027 Bahar Dönemi  
**Proje Konusu:** IT Service Desk / Servis Masası (ISO 9001 odaklı)  
**Teknoloji:** ASP.NET Core 8 MVC + Entity Framework Core (Code First) + SQL Server LocalDB + Bootstrap 5  
**Proje Durumu:** Geliştirme devam ediyor (henüz tamamlanmadı)  
**Task Manager:** [SEN0421 Project Management & Effort Tracker](https://docs.google.com/spreadsheets/d/12s66pasKcXvweL7JilKvezp6jXVdAZZhSq2FmvtDq34/edit?gid=0#gid=0) (Business Analyst — Google Sheets)

---

## Proje Durumu

Bu proje henüz tamamlanmamıştır; geliştirme ve iyileştirme çalışmaları devam etmektedir. Aşağıdaki özelliklerin bir kısmı çalışır durumda olsa da teslim / final sürümü değildir.

### Şu ana kadar hazır olanlar
- Talep (ticket) oluşturma, listeleme ve detay görüntüleme
- Önceliğe göre otomatik SLA hesaplama
- SLA ihlali / yaklaşan süre bildirimleri
- Dashboard KPI paneli (SLA uyum %, MTTR, CSAT vb.)
- Talep filtreleme (durum, öncelik, tür)
- Müşteri memnuniyeti (feedback / anket) altyapısı
- Bildirimler sayfası ve üst bar bildirim bileşeni
- Responsive sidebar arayüz (Bootstrap 5)

### Geliştirilecekler
- Kullanıcı girişi / kimlik doğrulama (Login şu an iskelet)
- Rol tabanlı yetkilendirme
- Eskalasyon ve atama akışlarının tamamlanması
- Chatbot entegrasyonunun tamamlanması

---

## Ekip Üyeleri

| Öğrenci No | Ad Soyad |
|------------|----------|
| 2100005226 | Emirhan Mollahüseyinoğlu |
| 2200005590 | Onur Akyüz |
| 2300006496 | Rana Neslihan Kurt |
| 2300007578 | Zeynep Avcı |

---

## Proje Açıklaması

ServiceDesk, ISO 9001 süreçlerine uygun bir IT servis masası uygulamasıdır. Kullanıcılar arıza (incident) ve değişiklik (change) talepleri açabilir; sistem önceliğe göre SLA sürelerini hesaplar, ihlalleri izler ve yöneticilere KPI odaklı bir panel sunar.

### Temel özellikler
- **Talepler:** Incident / Change türleri, öncelik seviyeleri, durum takibi
- **SLA:** Kritik 4s · Yüksek 8s · Orta 24s · Düşük 72s (seed politikası)
- **Dashboard:** Açık talepler, SLA ihlali, uyum oranı, MTTR, CSAT
- **Bildirimler:** SLA ihlali, yaklaşan süre, yeni talep uyarıları
- **Feedback:** Çözülen talepler için memnuniyet anketi

---

## Kurulum

Proje klasörü: `ServiceDesk/ServiceDesk`

### Tek tık
`kurulum.bat` dosyasına çift tıkla, ardından:

```bash
dotnet run
```

### Elle
```bash
cd ServiceDesk/ServiceDesk
dotnet restore
dotnet tool restore
dotnet ef database update
dotnet run
```

> Sadece .NET 9 kuruluysa önce: `set DOTNET_ROLL_FORWARD=Major`

---

## Klasör yapısı

| Klasör / dosya | İçerik |
|---|---|
| `Models/Entities.cs` | Ticket, SlaPolicy, Feedback + enum'lar |
| `Models/Ui.cs` | Badge renkleri ve Türkçe etiketler |
| `Data/AppDbContext.cs` | DbContext, ilişkiler, SLA seed |
| `Controllers/` | Home, Dashboard, Tickets, Notifications, Account |
| `Services/NotificationService.cs` | SLA / yeni talep bildirimleri |
| `ViewComponents/` | Üst bardaki bildirim zili |
| `Views/` | Razor arayüzü |
| `Migrations/` | EF Core migration'ları |

---

## Paketler

- Microsoft.EntityFrameworkCore.SqlServer 8.0.10
- Microsoft.EntityFrameworkCore.Design 8.0.10
- Microsoft.EntityFrameworkCore.Tools 8.0.10
- dotnet-ef 8.0.10 (yerel araç, `.config/dotnet-tools.json`)

---

## SLA politikası (seed)

| Öncelik | Çözüm süresi |
|---|---|
| Critical | 4 saat |
| High | 8 saat |
| Medium | 24 saat |
| Low | 72 saat |

---

© 2026–2027 SEN0421 Ekibi | İstanbul Kültür Üniversitesi
