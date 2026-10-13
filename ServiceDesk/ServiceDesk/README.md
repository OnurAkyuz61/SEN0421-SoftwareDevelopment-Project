# ServiceDesk – ISO 9001 IT Servis Masası (SDS-07)

ASP.NET Core 8 MVC + Entity Framework Core (Code First) + SQL Server LocalDB + Bootstrap 5

## Kurulum (tek tık)
`kurulum.bat` dosyasına çift tıkla, ardından:

```
dotnet run
```

## Kurulum (elle – derste gösterilecek komutlar)
```
dotnet restore
dotnet tool restore
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```
> Sadece .NET 9 kuruluysa önce: `set DOTNET_ROLL_FORWARD=Major`

## Uygulama bölümleri
| Bölüm | Adres | Kim kullanır |
|---|---|---|
| Karşılama | `/` | Herkes (giriş noktası) |
| Giriş + OTP | `/Account/Login` | Herkes |
| Kullanıcı Portalı | `/Portal` | Son kullanıcı: talep açar, dosya / ekran görüntüsü yükler, takip eder, anket doldurur |
| Yönetim Paneli | `/Dashboard` | Yönetici / Destek: grafikler, onaylar, atama, SLA, anketler, kullanıcılar |

## Klasör yapısı
| Dosya | İçerik |
|---|---|
| `Models/Entities.cs` | Ticket, Attachment, SlaPolicy, Feedback, AppUser + enum'lar |
| `Models/DashboardVm.cs` | Yönetim paneli KPI ve grafik verisi |
| `Models/Ui.cs` | Etiket renkleri ve Türkçe metinler |
| `Data/AppDbContext.cs` | DbContext, ilişkiler, seed verisi |
| `Services/FileStorage.cs` | Dosya yükleme (Uploads/ klasörü, uzantı + boyut kontrolü) |
| `Services/NotificationService.cs` | SLA / onay / yeni talep bildirimleri |
| `Controllers/PortalController.cs` | Kullanıcı portalı |
| `Controllers/AttachmentsController.cs` | Ek dosyayı açma / indirme |
| `Controllers/DashboardController.cs` | Yönetim: genel bakış (KPI + grafikler) |
| `Controllers/TicketsController.cs` | Yönetim: talep listesi, atama, durum |
| `Controllers/ApprovalsController.cs` | Yönetim: değişiklik onay / red |
| `Controllers/SurveysController.cs` | Yönetim: memnuniyet anketleri |
| `Controllers/UsersController.cs` | Yönetim: kullanıcı listesi |
| `Controllers/AccountController.cs` | Giriş + OTP doğrulama |
| `Views/Shared/_Layout.cshtml` | Yönetim paneli düzeni (sidebar) |
| `Views/Shared/_PortalLayout.cshtml` | Kullanıcı portalı düzeni (üst menü + chatbot) |
| `wwwroot/js/upload.js` | Sürükle-bırak, Ctrl+V ile ekran görüntüsü yapıştırma |
| `Uploads/` | Yüklenen dosyalar (git'e eklenmez) |

## Login bağlanırken yapılacaklar
- OTP sonrası role göre yönlendirme: `Admin/Agent` → `Dashboard`, `User` → `Portal`
- `PortalController`: listeyi oturumdaki kullanıcıya göre filtrele, `RequesterId` / `RequesterName` alanlarını oturumdan doldur
- Yönetim controller'larına (Dashboard, Tickets, Approvals, Surveys, Users, Notifications) sadece Admin/Agent erişsin
- Karşılama sayfasındaki ve menülerdeki `GEÇİCİ` linkler kaldırılacak

## Paketler
- Chart.js 4.4.1 (`wwwroot/lib/chartjs`, yerel)
- Microsoft.EntityFrameworkCore.SqlServer 8.0.10
- Microsoft.EntityFrameworkCore.Design 8.0.10
- Microsoft.EntityFrameworkCore.Tools 8.0.10
- dotnet-ef 8.0.10 (yerel araç, `.config/dotnet-tools.json`)

## SLA politikası (seed)
| Öncelik | Çözüm süresi |
|---|---|
| Critical | 4 saat |
| High | 8 saat |
| Medium | 24 saat |
| Low | 72 saat |
