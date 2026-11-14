using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Tests;

// Termin hatırlatmasının kuralı: hangi kayda, hangi aşamada ve KAÇ KEZ
// hatırlatma gider (ISO 9001 md. 10.2 — düzeltici faaliyetin takibi).
public class NonconformityReminderTests
{
    private const int BeforeHours = 48;

    private static readonly DateTime Now = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

    private static NonconformityReminderKind Kind(
        DateTime? dueAt, DateTime? reminderSentAt = null,
        NonconformityStatus status = NonconformityStatus.InProgress) =>
        NonconformityReminderService.KindFor(status, dueAt, reminderSentAt, Now, BeforeHours);

    // ───── Hangi kayıt hatırlatılır ──────────────────────────────────────

    [Fact]
    public void Kapanmis_kayit_hatirlatilmaz()
    {
        Assert.Equal(NonconformityReminderKind.None,
            Kind(Now.AddDays(-5), status: NonconformityStatus.Closed));
    }

    [Fact]
    public void Termini_olmayan_kayit_hatirlatilmaz()
    {
        Assert.Equal(NonconformityReminderKind.None, Kind(dueAt: null));
    }

    [Fact]
    public void Termin_uzaksa_hatirlatma_gitmez()
    {
        // 48 saatlik pencerenin dışında
        Assert.Equal(NonconformityReminderKind.None, Kind(Now.AddHours(72)));
    }

    [Fact]
    public void Termin_yaklasinca_bir_kez_hatirlatilir()
    {
        Assert.Equal(NonconformityReminderKind.DueSoon, Kind(Now.AddHours(36)));

        // Aynı aşamada ikinci kez gitmez
        Assert.Equal(NonconformityReminderKind.None,
            Kind(Now.AddHours(36), reminderSentAt: Now.AddHours(-1)));
    }

    [Fact]
    public void Termin_gecince_hic_hatirlatilmamis_kayit_gecikme_bildirimi_alir()
    {
        Assert.Equal(NonconformityReminderKind.Overdue, Kind(Now.AddHours(-2)));
    }

    // "Yaklaşıyor" hatırlatması gitmiş olsa bile gecikme AYRI bir aşamadır:
    // tek alanla (ReminderSentAt) ayrım, işaretin termine göre konumundan okunur
    [Fact]
    public void Yaklasiyor_hatirlatmasi_gitse_de_gecikme_ayrica_bildirilir()
    {
        var due = Now.AddHours(-2);

        Assert.Equal(NonconformityReminderKind.Overdue,
            Kind(due, reminderSentAt: due.AddHours(-10)));   // termin ÖNCESİ gönderilmiş
    }

    [Fact]
    public void Gecikme_bildirimi_bir_kez_gider()
    {
        var due = Now.AddHours(-10);

        Assert.Equal(NonconformityReminderKind.None,
            Kind(due, reminderSentAt: due.AddHours(1)));     // termin SONRASI gönderilmiş
    }

    // ───── Mesaj ─────────────────────────────────────────────────────────

    [Fact]
    public void Yaklasan_ve_gecen_termin_icin_ayri_mesaj_uretilir()
    {
        var n = new Nonconformity { Id = 7, Description = "SLA aşıldı.", DueAt = Now.AddHours(12) };

        var yaklasan = NonconformityReminderService.Compose(n, NonconformityReminderKind.DueSoon, Now);
        Assert.Contains("yaklaşıyor", yaklasan.Title);
        Assert.Contains("12 saat", yaklasan.Message);
        Assert.Equal(EmailTemplates.NonconformityReminder, yaklasan.TemplateKey);

        n.DueAt = Now.AddDays(-3);
        var gecen = NonconformityReminderService.Compose(n, NonconformityReminderKind.Overdue, Now);
        Assert.Contains("geçti", gecen.Title);
        Assert.Contains("3 gün", gecen.Message);
        Assert.Equal(EmailTemplates.NonconformityOverdue, gecen.TemplateKey);

        // İki mesaj da aynı kaydı gösteriyor ama metinleri farklı
        Assert.NotEqual(yaklasan.Title, gecen.Title);
    }

    [Fact]
    public void Sure_metni_dakika_saat_ve_gun_olarak_yazilir()
    {
        Assert.Equal("30 dakika", NonconformityReminderService.Elapsed(Now, Now.AddMinutes(30)));
        Assert.Equal("5 saat", NonconformityReminderService.Elapsed(Now, Now.AddHours(5)));
        Assert.Equal("2 gün", NonconformityReminderService.Elapsed(Now, Now.AddDays(2)));
        Assert.Equal("0 dakika", NonconformityReminderService.Elapsed(Now, Now.AddHours(-1)));
    }

    // Yeni bildirim türü de uygulama içi kanalı kapatılamayan türlerden:
    // kalite kaydının sahibi "görmedim" diyemez
    [Fact]
    public void Termin_hatirlatmasinda_uygulama_ici_bildirim_kapatilamaz()
    {
        var kapatmaya_calis = new NotificationPreference
        {
            Type = NotificationType.NonconformityDueReminder,
            InApp = false, Email = false, Sms = true
        };

        var ch = NotificationChannelPolicy.Resolve(
            NotificationType.NonconformityDueReminder, kapatmaya_calis, hasEmail: true, hasPhone: true);

        Assert.True(ch.InApp);
        Assert.False(ch.Email);
        Assert.True(ch.Sms);
    }
}
