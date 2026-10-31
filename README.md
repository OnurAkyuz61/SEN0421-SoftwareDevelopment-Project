# ServiceDesk – ISO 9001 IT Servis Masası (SDS-07)

## Proje Bilgileri

**Üniversite:** İstanbul Kültür Üniversitesi  
**Ders:** SEN0421 - Software Development with Standards  
**Dönem:** 2026-2027 Bahar Dönemi  
**Proje Konusu:** IT Service Desk / Servis Masası (ISO 9001 odaklı)  
**Teknoloji:** ASP.NET Core 8 MVC + Entity Framework Core (Code First) + SQL Server LocalDB + Bootstrap 5  
**Proje Durumu:** Geliştirme devam ediyor  
**Task Manager:** [SEN0421 Project Management & Effort Tracker](https://docs.google.com/spreadsheets/d/12s66pasKcXvweL7JilKvezp6jXVdAZZhSq2FmvtDq34/edit?gid=0#gid=0) (Business Analyst — Google Sheets)

---

## Proje Durumu

Uygulamanın temel akışları çalışır durumda; Sprint 2’de güvenlik, doğrulama, sayfalama ve arşiv gibi iyileştirmeler yapıldı. Teslim / final sürümü değildir.

### Hazır olanlar
- Giriş: e-posta + şifre → OTP kanal seçimi → 6 haneli kod; rol bazlı yönlendirme (`Admin`/`Agent` → Dashboard, `User` → Portal)
- Kullanıcı portalı: talep açma, dosya / ekran görüntüsü yükleme, takip, memnuniyet anketi
- Yönetim paneli: talep listesi (filtre + sayfalama), atama, durum, arşiv, onaylar, SLA, KPI dashboard
- SLA hesaplama + arka plan izleme (`SlaMonitorService`): uyarı, ihlal, eskalasyon (yeniden atama + bildirim)
- Değişiklik yönetimi: ChangeDetail, çok adımlı onay (yüksek risk)
- ISO kalite katmanı: uygunsuzluklar, bilgi bankası, memnuniyet anketleri
- Katalog (kategori / birim / ekip), eskalasyon kuralları, kullanıcı yönetimi
- Soft-delete arşiv ekranı; şifre karmaşıklığı, dosya yükleme doğrulaması, eşzamanlı düzenleme koruması
- Birim testleri (`ServiceDesk.Tests`)

### Bilinen kısıtlar / sonraki adımlar
- OTP SMS/WhatsApp ve e-posta gönderimi simülasyon (Development’ta demo kod / log)
- Chatbot UI hazır ama henüz bağlı değil (layout’tan gizlendi)
- Gerçek SMTP / SMS sağlayıcı entegrasyonu

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

ServiceDesk, ISO 9001 süreçlerine uygun bir IT servis masası uygulamasıdır. Kullanıcılar arıza (incident) ve değişiklik (change) talepleri açabilir; sistem önceliğe göre SLA sürelerini hesaplar, ihlalleri izler, eskalasyon uygular ve yöneticilere KPI odaklı bir panel sunar.

### Temel özellikler
- **Talepler:** Incident / Change, öncelik, durum, yorum, ek dosya, soft-delete arşiv
- **SLA:** Kritik 4s · Yüksek 8s · Orta 24s · Düşük 72s (seed politikası)
- **Eskalasyon:** SLA aşımında hedef role yeniden atama + bildirim
- **Dashboard:** Açık talepler, SLA ihlali, uyum oranı, MTTR, CSAT
- **Bildirimler:** SLA, onay, atama, eskalasyon (uygulama içi; e-posta stub)
- **Anket:** Çözülen talepler için memnuniyet değerlendirmesi
- **Yetki:** `AuthorizeRole` — User → Portal; Agent/Admin → yönetim

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

> Sadece .NET 9/10 kuruluysa önce: `set DOTNET_ROLL_FORWARD=Major` (Windows) veya `export DOTNET_ROLL_FORWARD=Major` (macOS/Linux)

### Testler
```bash
cd ServiceDesk
dotnet test ServiceDesk.Tests/ServiceDesk.Tests.csproj
```

### Demo hesaplar (yalnızca Development)
| E-posta | Şifre | Rol |
|---|---|---|
| `admin@servicedesk.local` | `admin123` | Yönetici |
| `agent@servicedesk.local` | `agent123` | Destek |
| `user@servicedesk.local` | `user123` | Kullanıcı |

Development’ta giriş sayfasında şifresiz **DevLogin** ve OTP demo kodu yalnızca bu bilgisayardan görünür. Yeni belirlenen şifrelerde karmaşıklık kuralı zorunludur (büyük/küçük harf, rakam, özel karakter).

---

## Klasör yapısı (özet)

| Klasör / dosya | İçerik |
|---|---|
| `Models/` | Entity’ler, form VM’leri, UI etiketleri |
| `Data/AppDbContext.cs` | DbContext, ilişkiler, seed, soft-delete filtreleri |
| `Controllers/` | Portal, Dashboard, Tickets, Approvals, Account, Users, … |
| `Services/` | SLA, KPI, auth kuralları, dosya yükleme, sayfalama, eskalasyon |
| `Filters/` | `AuthorizeRole`, `DevelopmentOnly` |
| `Views/` | Yönetim / portal / auth Razor görünümleri |
| `Migrations/` | EF Core migration’ları |
| `../ServiceDesk.Tests/` | Birim testleri |

Ayrıntılı geliştirici notları: [`ServiceDesk/ServiceDesk/README.md`](ServiceDesk/ServiceDesk/README.md)

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
