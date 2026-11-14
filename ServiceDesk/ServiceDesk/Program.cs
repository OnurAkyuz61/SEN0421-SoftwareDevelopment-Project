using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Search;
using ServiceDesk.Services.Approvals;
using ServiceDesk.Services.Escalations;
using ServiceDesk.Services.Messaging;
using ServiceDesk.Services.Notifications;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// ───── EK DOSYALAR (Uploads/) ────────────────────────────────────────────
// Boyut sınırları ve kota appsettings.json → "Uploads" bölümünden gelir.
builder.Services.Configure<UploadSettings>(builder.Configuration.GetSection(UploadSettings.Section));
builder.Services.AddSingleton<IAttachmentScanner, LoggingAttachmentScanner>(); // antivirüs stub (loglar)
builder.Services.AddSingleton<FileStorage>(); // talep ekleri (Uploads/ klasörü)
builder.Services.AddScoped<AttachmentQuota>();        // kullanıcı / talep başına depolama kotası
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SessionRegistry>();       // eşzamanlı oturum kaydı / limiti
builder.Services.AddScoped<CurrentUser>();           // oturumdaki kullanıcı
builder.Services.AddScoped<NotificationService>();   // bildirim okuma / yazma
builder.Services.AddScoped<TicketSlaService>();      // SLA politikası ve hedef tarihler
builder.Services.AddScoped<TeamRoundRobin>();        // ekip bazlı otomatik (sıralı) atama
builder.Services.AddScoped<KpiService>();            // dönemsel gösterge hesabı
builder.Services.AddScoped<ManagementReviewService>();// yönetim gözden geçirme paketi (md. 9.3, salt okuma)
builder.Services.AddScoped<SearchService>();         // bilgi bankası / talep / global arama
// Uygunsuzluk eşikleri (ortalama + kritik soru puanı) ve termin:
// appsettings.json → "Quality"
builder.Services.Configure<QualitySettings>(builder.Configuration.GetSection(QualitySettings.Section));
builder.Services.AddScoped<NonconformityService>();  // otomatik uygunsuzluk kaydı
builder.Services.AddHostedService<NonconformityReminderService>(); // termin hatırlatma job'ı
builder.Services.AddScoped<LoginAuditService>();     // giriş denetim izi ve deneme sınırı
builder.Services.AddScoped<AuditService>();          // güvenlik / süreç denetim kaydı
builder.Services.AddMemoryCache();

// ───── MESAJLAŞMA (SMS / WhatsApp / e-posta) ─────────────────────────────
// Ayarlar appsettings.json → "Messaging". Gerçek sağlayıcı eklendiğinde
// yalnızca buradaki kayıt değişir; arayüzler (ISmsSender, IWhatsAppSender,
// IEmailSender) sabit kalır.
builder.Services.Configure<MessagingSettings>(builder.Configuration.GetSection(MessagingSettings.Section));
builder.Services.Configure<NotificationSettings>(builder.Configuration.GetSection(NotificationSettings.Section));
builder.Services.Configure<ApprovalSettings>(builder.Configuration.GetSection(ApprovalSettings.Section));
builder.Services.AddSingleton<ISmsSender, LoggingSmsSender>();           // SMS stub (loglar)
builder.Services.AddSingleton<IWhatsAppSender, LoggingWhatsAppSender>(); // WhatsApp stub (loglar)
var emailMode = builder.Configuration[$"{MessagingSettings.Section}:Email:Mode"] ?? "Logging";
if (string.Equals(emailMode, "Smtp", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();      // gerçek SMTP
else
    builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();   // geliştirme: loga yazar
builder.Services.AddScoped<OtpDeliveryService>();

// ───── BİLDİRİM KUYRUĞU ─────────────────────────────────────────────────
builder.Services.AddSingleton<UnreadCountCache>();      // okunmamış sayaç önbelleği
builder.Services.AddSingleton<MessageQueueSignal>();    // worker'ı uyandırma sinyali
builder.Services.AddScoped<NotificationDispatcher>();   // e-posta / SMS gönderimi + yeniden deneme
builder.Services.AddHostedService<MessageQueueWorker>();// ayrı kuyruk worker'ı
builder.Services.AddHostedService<DailyDigestService>();// günlük özet e-postası

// ───── ESKALASYON VE ONAY ───────────────────────────────────────────────
builder.Services.AddScoped<EscalationService>();        // otomatik + manuel eskalasyon
builder.Services.AddScoped<ApprovalAuthority>();        // vekalet + onay bağlamı
builder.Services.AddScoped<ApprovalChainResolver>();    // zincir kuralı çözümü
builder.Services.AddScoped<ChangeResubmissionService>();// reddedileni düzeltip yeniden gönderme
builder.Services.AddHostedService<ApprovalReminderService>(); // onay hatırlatma job'ı

builder.Services.AddHostedService<SlaMonitorService>();   // dakikada bir SLA taraması
builder.Services.AddHostedService<KpiAggregatorService>();// yarım saatte bir KPI

// ───── MEMNUNİYET ANKETİ ────────────────────────────────────────────────
// Hatırlatma gecikmesi ve anketin doldurulma süresi (TTL) appsettings.json →
// "Survey" bölümünden gelir; PortalController süresi dolan anketi reddeder.
builder.Services.Configure<SurveySettings>(builder.Configuration.GetSection(SurveySettings.Section));
builder.Services.AddHostedService<SurveyReminderService>();   // anket hatırlatma e-postası

// ───── EK DOSYA BAKIM JOB'LARI ──────────────────────────────────────────
// Süreler ve açma / kapama appsettings.json → "Uploads" bölümünden gelir.
builder.Services.AddHostedService<AttachmentRetentionService>();   // saklama süresi dolan ekleri kalıcı sil
builder.Services.AddHostedService<OrphanAttachmentScanService>();  // kaydı olmayan dosyaları raporla

// ───── İSTEK SINIRI (arama ve OTP uçları) ───────────────────────────────
builder.Services.AddServiceDeskRateLimiting(builder.Configuration);
// ───── OTURUM (SESSION) ──────────────────────────────────────────────────
// Süreler ve OTP sınırları appsettings.json → "Auth" bölümünden gelir
var authSection = builder.Configuration.GetSection(AuthSettings.Section);
builder.Services.Configure<AuthSettings>(authSection);
var auth = authSection.Get<AuthSettings>() ?? new AuthSettings();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromMinutes(auth.IdleTimeoutMinutes);
    opt.Cookie.Name = AuthSettings.CookieName;
    opt.Cookie.HttpOnly = true;                       // JavaScript okuyamaz
    opt.Cookie.IsEssential = true;
    opt.Cookie.SameSite = SameSiteMode.Lax;           // başka sitelerden gelen POST'larda gönderilmez
    opt.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest            // yerelde http ile de çalışsın
        : CookieSecurePolicy.Always;                  // canlıda yalnızca https
});

var app = builder.Build();

// ───── GELİŞTİRME KOLAYLIĞI ───────────────────────────────────────────────
// Panel ve raporların dolu görünmesi için örnek veri üretir. Yalnızca
// Development'ta ve veritabanı boşsa çalışır. Canlıya çıkmadan önce bu blok
// ve Data/DevDataSeeder.cs silinecek.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DevDataSeeder");
    try
    {
        await DevDataSeeder.SeedAsync(db, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Örnek veri üretilemedi");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Gövdesiz hata yanıtları (404, 403, 413 …) da aynı Türkçe hata sayfasına
// düşer: NotFound() dönen bir action artık tarayıcının boş "404" ekranını
// değil, ne olduğunu anlatan ve kullanıcıyı kendi alanına geri gönderen
// sayfayı gösterir. Durum kodu korunur.
app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");

// Tarayıcı, sunucunun bildirdiği içerik türünü "tahmin" ederek değiştiremez
// (ör. HTML içeren bir metin dosyasını sayfa gibi çalıştıramaz)
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseRateLimiter();   // session'dan sonra: bölüm anahtarı kullanıcı / bekleyen girişten üretilir
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
