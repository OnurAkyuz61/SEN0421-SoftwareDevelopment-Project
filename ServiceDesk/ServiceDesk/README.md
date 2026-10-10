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

## Klasör yapısı
| Klasör | İçerik |
|---|---|
| `Models/Entities.cs` | Ticket, SlaPolicy, Feedback varlıkları + enum'lar |
| `Models/Ui.cs` | Badge renkleri ve Türkçe etiketler |
| `Data/AppDbContext.cs` | DbContext, ilişkiler, SLA seed verisi |
| `Controllers/HomeController.cs` | Ön karşılama sayfası (`/`) |
| `Controllers/DashboardController.cs` | KPI paneli (SLA uyum %, MTTR, CSAT) |
| `Controllers/TicketsController.cs` | Talep listesi + filtre, SLA hesaplama, eskalasyon, anket |
| `Controllers/NotificationsController.cs` | Bildirimler sayfası |
| `Controllers/AccountController.cs` | Login (boş, geliştirilecek) |
| `Services/NotificationService.cs` | SLA ihlali / yaklaşan / yeni talep bildirimleri |
| `ViewComponents/` | Üst bardaki bildirim zili |
| `Views/` | Razor arayüzü (sidebar layout) |
| `Migrations/` | `dotnet ef migrations add` ile oluşur |

## Paketler
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
