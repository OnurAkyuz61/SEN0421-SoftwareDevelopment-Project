# ServiceDesk – ISO 9001 IT Servis Masası (SDS-07)

ASP.NET Core 8 MVC + Entity Framework Core (Code First) + SQL Server LocalDB + Bootstrap 5

## Kurulum (tek tık)
`kurulum.bat` dosyasına çift tıkla, ardından:

```
dotnet run
```

## Kurulum (elle)
```
dotnet restore
dotnet tool restore
dotnet ef database update
dotnet run
```
> Sadece .NET 9/10 kuruluysa önce: `set DOTNET_ROLL_FORWARD=Major` (Windows) / `export DOTNET_ROLL_FORWARD=Major` (macOS/Linux)

## Testler
```
cd ..
dotnet test ServiceDesk.Tests/ServiceDesk.Tests.csproj
```

## Uygulama bölümleri
| Bölüm | Adres | Kim kullanır |
|---|---|---|
| Karşılama | `/` | Herkes (giriş noktası) |
| Giriş + OTP | `/Account/Login` | Herkes |
| Şifre sıfırlama | `/Account/ForgotPassword` | Herkes |
| Kullanıcı Portalı | `/Portal` | Son kullanıcı: talep açar, dosya / ekran görüntüsü yükler, takip eder, anket doldurur |
| Bilgi Bankası | `/Knowledge` | Herkes (yazma: Destek/Yönetici) |
| Yönetim Paneli | `/Dashboard` | Yönetici / Destek: KPI, onaylar, atama, SLA, anketler, kullanıcılar |
| Talepler | `/Tickets` | Destek / Yönetici (filtre + sayfalama) |
| Arşiv | `/Tickets/Archived` | Destek / Yönetici (soft-delete edilmiş talepler) |
| Profil | `/Profile` | Oturum açmış herkes |

## Auth ve roller
- Akış: e-posta + şifre → OTP kanalı (SMS/WhatsApp simülasyonu) → 6 haneli kod → oturum
- OTP sonrası yönlendirme: `Admin`/`Agent` → `Dashboard`, `User` → `Portal`
- Yetki: `[AuthorizeRole(...)]` — yönetim controller’ları yalnızca Destek/Yönetici
- DevLogin ve OTP demo kodu: yalnızca Development + bu bilgisayar (`DevelopmentOnlyAttribute`)
- E-posta gönderimi: `LoggingEmailSender` (SMTP yok; bağlantı Development’ta ekranda gösterilebilir)
- Yeni şifre kuralı (`PasswordRules`): en az 8 karakter + büyük/küçük harf + rakam + özel karakter

## Oturum (session) kuralları
Değerler `appsettings.json` → `Auth` bölümünden değiştirilebilir.

| Kural | Varsayılan |
|---|---|
| İşlem yapılmazsa oturum kapanır | 30 dk (2 dk kala uyarı penceresi) |
| Oturumun en uzun süresi | 8 saat |
| Şifreden sonra OTP adımı için süre | 10 dk |
| Kodun geçerliliği | 5 dk |
| "Tekrar gönder" bekleme süresi | 30 sn |
| Bir girişte en fazla kod gönderimi | 4 |
| Aynı kod için en fazla hatalı deneme | 5 |

## SLA ve eskalasyon
| Öncelik | Çözüm süresi |
|---|---|
| Critical | 4 saat |
| High | 8 saat |
| Medium | 24 saat |
| Low | 72 saat |

`SlaMonitorService` dakikada bir tarar: yaklaşan SLA uyarısı, ihlal işareti, uygunsuzluk kaydı ve eskalasyon kuralına göre **yeniden atama + bildirim** (hedef rolde en az açık talebi olan aktif hesap).

## Önemli davranışlar
- **Soft-delete:** Talep silinmez; arşivlenir. Normal listelerde görünmez; `/Tickets/Archived` ve detayda salt okunur açılır.
- **Sayfalama:** Talepler, kullanıcılar, bilgi bankası, anketler — sayfa başına 20 (`Paging` / `_Pager`).
- **Dosya yükleme:** Uzantı + içerik imzası (`UploadPolicy`); görseller güvenli başlıklarla sunulur.
- **Eşzamanlı düzenleme:** `Ticket.RowVersion` + `TicketConcurrency`.
- **Katalog:** Kategori / birim / ekip adları benzersiz; ekip lideri yalnızca aktif Agent/Admin.
- **Chatbot:** UI iskeleti var, henüz API yok; layout’lardan gizlendi (`_ChatbotPartial`).

## Klasör yapısı
| Dosya / klasör | İçerik |
|---|---|
| `Models/Entities.cs` | Ticket, Attachment, SlaPolicy, AppUser, Change*, Survey*, NC, Knowledge + enum’lar |
| `Models/Ui.cs` | Etiket renkleri ve Türkçe metinler |
| `Data/AppDbContext.cs` | DbContext, ilişkiler, seed, soft-delete query filter |
| `Data/DevDataSeeder.cs` | Development demo kullanıcı / örnek veri |
| `Services/SlaMonitorService.cs` | SLA tarama + eskalasyon (arka plan) |
| `Services/SlaCalculator.cs` / `TicketSlaService.cs` | İş takvimi ile SLA süreleri |
| `Services/UploadPolicy.cs` / `FileStorage.cs` | Dosya doğrulama ve saklama |
| `Services/PasswordRules.cs` / `PhoneRules.cs` / `CatalogRules.cs` | Doğrulama kuralları |
| `Services/Paging.cs` | Ortak sayfalama |
| `Services/TicketConcurrency.cs` | Eşzamanlı düzenleme |
| `Services/ApprovalPolicy.cs` / `TicketWorkflow.cs` | Onay ve durum geçişleri |
| `Services/NotificationService.cs` | Uygulama içi (+ e-posta stub) bildirimler |
| `Services/EmailSender.cs` | Loglayan e-posta gönderici |
| `Filters/AuthorizeRoleAttribute.cs` | Rol bazlı erişim |
| `Filters/DevelopmentOnlyAttribute.cs` | DevLogin / demo OTP kilidi |
| `Controllers/PortalController.cs` | Kullanıcı portalı |
| `Controllers/TicketsController.cs` | Liste, detay, atama, durum, arşiv |
| `Controllers/ApprovalsController.cs` | Değişiklik onay / red |
| `Controllers/AccountController.cs` | Giriş, OTP, şifre sıfırlama |
| `Controllers/UsersController.cs` | Kullanıcı CRUD + giriş denemeleri (Admin) |
| `Controllers/CatalogController.cs` | Kategori / birim / ekip |
| `Controllers/EscalationRulesController.cs` | Eskalasyon kuralları |
| `Controllers/KnowledgeController.cs` | Bilgi bankası |
| `Controllers/NonconformitiesController.cs` | Uygunsuzluklar |
| `Controllers/SurveysController.cs` | Memnuniyet anket sonuçları |
| `Views/Shared/_Layout.cshtml` | Yönetim paneli (sidebar) |
| `Views/Shared/_PortalLayout.cshtml` | Kullanıcı portalı |
| `Views/Shared/_AuthLayout.cshtml` | Giriş / OTP |
| `Views/Shared/_Pager.cshtml` | Ortak sayfalama çubuğu |
| `Views/Shared/_SessionTimeout.cshtml` | Oturum süresi uyarısı |
| `wwwroot/css/theme.css` | Ortak tema değişkenleri |
| `wwwroot/css/site.css` / `auth.css` | Panel, portal, giriş stilleri |
| `wwwroot/js/site.js` / `auth.js` / `upload.js` | Menü, OTP UI, sürükle-bırak yükleme |
| `Uploads/` | Yüklenen dosyalar (git’e eklenmez) |

## Demo hesaplar (Development)
| E-posta | Şifre | Rol |
|---|---|---|
| `admin@servicedesk.local` | `admin123` | Yönetici |
| `agent@servicedesk.local` | `agent123` | Destek |
| `user@servicedesk.local` | `user123` | Kullanıcı |

## Paketler
- Chart.js 4.4.1 (`wwwroot/lib/chartjs`, yerel)
- Microsoft.EntityFrameworkCore.SqlServer 8.0.10
- Microsoft.EntityFrameworkCore.Design 8.0.10
- Microsoft.EntityFrameworkCore.Tools 8.0.10
- dotnet-ef 8.0.10 (yerel araç, `.config/dotnet-tools.json`)
