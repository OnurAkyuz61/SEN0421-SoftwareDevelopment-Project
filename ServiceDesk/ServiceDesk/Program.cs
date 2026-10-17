using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;
using ServiceDesk.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<FileStorage>(); // talep ekleri (Uploads/ klasörü)
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();           // oturumdaki kullanıcı
builder.Services.AddScoped<NotificationService>();   // bildirim okuma / yazma
builder.Services.AddScoped<TicketSlaService>();      // SLA politikası ve hedef tarihler
builder.Services.AddScoped<KpiService>();            // dönemsel gösterge hesabı
builder.Services.AddScoped<NonconformityService>();  // otomatik uygunsuzluk kaydı
builder.Services.AddScoped<LoginAuditService>();     // giriş denetim izi ve deneme sınırı
builder.Services.AddHostedService<SlaMonitorService>();   // dakikada bir SLA taraması
builder.Services.AddHostedService<KpiAggregatorService>();// yarım saatte bir KPI
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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
