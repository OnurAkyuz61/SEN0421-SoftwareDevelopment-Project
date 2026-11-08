# Yeni özellikler (Kasım 2026)

Kurulum: veritabanını güncellemek için proje klasöründe

```
dotnet ef database update
```

(yeni migration: `20261108191900_NotificationsEscalationApprovals`). Ayarlar `appsettings.json` içinde
`Messaging`, `Notifications`, `Approvals`, `RateLimiting` bölümlerindedir.

## OTP / SMS / WhatsApp
| Görev | Nerede |
|---|---|
| Başarısız OTP denemelerinin denetim kaydı | `AccountController.OtpVerify` → `AuditLogs` (hatalı kod, süresi dolmuş kod, deneme sınırı, hesap kilidi, gönderim sınırı). Ekran: **Yönetim → Denetim kaydı** |
| SMS arayüzü + loglayan stub | `Services/Messaging/TextSenders.cs` → `ISmsSender`, `LoggingSmsSender` |
| WhatsApp stub | aynı dosya → `IWhatsAppSender`, `LoggingWhatsAppSender` |
| OTP gönderimlerinin veritabanına kaydı | `OtpDeliveryService` → `OtpDeliveryLogs` tablosu (kod saklanmaz, telefon maskeli) |
| Kanala göre teslim durumu | `OtpDeliveryStatus` (Kuyrukta/Gönderildi/Teslim edildi/Okundu/Başarısız/Kanal kapalı), `OtpDeliveryService.UpdateStatusAsync` (sağlayıcı webhook'u için). Ekran: **Denetim kaydı → OTP gönderimleri** (kanal bazında teslim oranı) |
| SMS/WhatsApp ayarları appsettings'te | `Messaging:Sms`, `Messaging:WhatsApp` (Enabled, Provider, SenderId, ApiKey, şablon…). Kapalı kanal OTP ekranında gizlenir |

## E-posta ve bildirimler
| Görev | Nerede |
|---|---|
| Gerçek SMTP göndericisi | `SmtpEmailSender` — `Messaging:Email:Mode = "Smtp"` yapılınca devreye girer |
| Kullanıcı bazlı e-posta tercihleri | `NotificationPreferences` tablosu + **Profilim → Bildirim tercihleri**; günlük özet aç/kapat |
| Türe göre kanal seçimi (uygulama içi / e-posta / SMS) | `NotificationChannelPolicy` (varsayılanlar, kilitli kanallar, SMS izinli türler) |
| Başarısız e-postalar için yeniden deneme kuyruğu | `Notification.AttemptCount/NextAttemptAt/IsFailed` + `DeliveryRetryPolicy` (üstel bekleme). Ekran: **Yönetim → E-posta / SMS kuyruğu** (elle yeniden dene) |
| Günlük özet e-posta job'ı | `DailyDigestService` (her gün `DigestHourLocal` saatinde, kullanıcı başına bir kez) |
| Okunmamış sayısı için hafif API | `GET /Notifications/UnreadCount` → `{"count": n}`; zil 60 sn'de bir yoklar |
| Toplu okundu işaretlemenin sayfalı yapılması | Bildirim listesi sayfalı; "Bu sayfayı okundu say" (`MarkPageRead`) ve "Tümü" 500'lük partilerle SQL UPDATE |
| Atama / yorum / SLA için ayrı e-posta şablonları | `EmailTemplates` (assignment, comment, sla.warning, sla.breach, approval.*, digest…) |
| E-posta/SMS kuyruk worker'ının ayrılması | `MessageQueueWorker` (BackgroundService) — istekler artık gönderimi beklemez, `SignalDelivery()` ile worker uyandırılır |
| Okunmamış sayısının cache'lenmesi | `UnreadCountCache` (IMemoryCache, 60 sn TTL; bildirim yazılınca `AppDbContext` otomatik geçersiz kılar) |

## Eskalasyon
| Görev | Nerede |
|---|---|
| Ekip liderine yükseltme | `EscalationRule.TargetType = TeamLead` → talebin ekibinin `LeadUser`'ı |
| Manuel eskalasyon endpoint'i | `POST /Tickets/Escalate/{id}` (gerekçe zorunlu, isteğe bağlı hedef kişi). Talep detayında form |
| Eskalasyon geçmişi raporu | **Eskalasyon raporu** (`/Reports/Escalations`): seviye, kişi, ekip dağılımı, manuel/otomatik, CSV dışa aktarma |
| Seviyeye göre farklı bildirim şablonları | `escalation.l1`, `escalation.l2`, `escalation.l3` (3 ve üzeri "KRİTİK") |
| Eskalasyon sonrası öncelik artışı | `EscalationRule.RaisePriority` (Kritik'te durur), kayıtta önceki/sonraki öncelik |
| Hedef rolde kimse yoksa yedek atama | `FallbackUserId` → `FallbackRole` sırası; kayıtta "yedek" işareti |

Ortak mantık `Services/Escalations/EscalationService.cs` içinde; `SlaMonitorService` artık bunu çağırıyor.

## Değişiklik onayı
| Görev | Nerede |
|---|---|
| Bekleyen onaylar için hatırlatma job'ı | `ApprovalReminderService` (`ReminderAfterHours`, `ReminderIntervalHours`) |
| Onay yetkisinin vekaleti | `ApprovalDelegations` + **Onaylar → Vekalet**; kararda "X adına vekaleten" kaydı (`ChangeApproval.OnBehalfOfId`) |
| Planlanan pencere dışında onay engeli | `ApprovalPolicy` — bitişi geçmiş pencerede onay verilmez (`BlockOutsidePlannedWindow`, isteğe bağlı `RequirePlannedWindow`); uygulama da yalnızca pencere içinde başlar |
| Onay sonrası uygulama / geri alma alanları | `ChangeDetail.ImplementationStatus` (+ zaman damgaları, not) ve `ChangeImplementationRules` geçişleri; talep detayında panel |
| Reddedileni düzeltip yeniden gönderme | `ChangeResubmissionService` — yeni onay turu (`ApprovalRound`), düzeltme notu yazışmaya eklenir, en fazla `MaxResubmissions`; portal ve yönetim ekranında form. Red gerekçesi artık zorunlu |
| Yüksek risk için CAB toplantı kaydı | `ChangeDetail.CabMeetingUrl/CabMeetingAt`; yüksek riskte nihai onay bunu ister (`RequireCabForHighRisk`) |
| Zincir adımlarının risk dışında da yapılandırılması | `ApprovalChainRules` (kategori, en az öncelik, risk → adım sayısı) + **Onaylar → Zincir kuralları** |
| Bekleyen onayların filtreli ve sayfalı listesi | `/Approvals` — arama, risk, öncelik, kategori, tarih, yeniden gönderilen, penceresi geçmiş, sıralama |

## Güvenlik
| Görev | Nerede |
|---|---|
| Arama ve OTP uçlarına rate limiting | `Services/RateLimiting.cs` — `otp` (IP + bekleyen giriş, sabit pencere) ve `search` (kullanıcı, kayan pencere) politikaları; aşımda 429 + denetim kaydı |

## Testler
`ServiceDesk.Tests/NewFeatureTests.cs` — vekalet, pencere, CAB, tur, zincir kuralı, yeniden gönderim, uygulama geçişleri,
hatırlatma zamanı, öncelik artışı, yeniden deneme, kanal seçimi, şablonlar, günlük özet saati. Toplam 151 test geçiyor.
