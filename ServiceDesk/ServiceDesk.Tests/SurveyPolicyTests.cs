using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Anket penceresi: ne zaman doldurulabilir, hatırlatma ne zaman gider,
// süresi dolan anket neden reddedilir.
public class SurveyPolicyTests
{
    private const int ReminderAfterHours = 24;
    private const int ExpiresAfterDays = 14;

    private static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    private static Ticket Resolved(double hoursAgo, DateTime? reminderSentAt = null,
                                   SurveyResponse? survey = null)
    {
        var resolvedAt = Now.AddHours(-hoursAgo);
        return new Ticket
        {
            Status = TicketStatus.Resolved,
            ResolvedAt = resolvedAt,
            SurveyInvitedAt = resolvedAt,
            SurveyReminderSentAt = reminderSentAt,
            Survey = survey
        };
    }

    // ───── Doldurma süresi (TTL) ─────────────────────────────────────────

    [Fact]
    public void Sure_icinde_anket_doldurulabilir()
    {
        var t = Resolved(hoursAgo: 48);

        Assert.False(SurveyPolicy.IsExpired(t, Now, ExpiresAfterDays));
        Assert.True(SurveyPolicy.CanSubmit(t, Now, ExpiresAfterDays));
    }

    [Fact]
    public void Suresi_dolan_anket_reddedilir()
    {
        var t = Resolved(hoursAgo: 15 * 24);   // 15 gün önce çözüldü, süre 14 gün

        Assert.True(SurveyPolicy.IsExpired(t, Now, ExpiresAfterDays));
        Assert.False(SurveyPolicy.CanSubmit(t, Now, ExpiresAfterDays));
    }

    [Fact]
    public void Son_gun_sinirinda_anket_hala_acik()
    {
        // Tam sınırda: süre "geçmiş" değil
        var t = Resolved(hoursAgo: ExpiresAfterDays * 24);

        Assert.False(SurveyPolicy.IsExpired(t, Now, ExpiresAfterDays));
    }

    [Fact]
    public void Sure_sinirsizsa_anket_hic_kapanmaz()
    {
        var t = Resolved(hoursAgo: 365 * 24);

        Assert.Null(SurveyPolicy.Deadline(t, expiresAfterDays: 0));
        Assert.False(SurveyPolicy.IsExpired(t, Now, expiresAfterDays: 0));
    }

    [Fact]
    public void Cozulmemis_talepte_anket_penceresi_yok()
    {
        var t = new Ticket { Status = TicketStatus.InProgress };

        Assert.Null(SurveyPolicy.Deadline(t, ExpiresAfterDays));
        // Süresi "dolmuş" değil: henüz açılmamış
        Assert.False(SurveyPolicy.IsExpired(t, Now, ExpiresAfterDays));
        Assert.False(SurveyPolicy.CanSubmit(t, Now, ExpiresAfterDays));
    }

    [Fact]
    public void Eski_kayitlarda_pencere_cozum_tarihinden_sayilir()
    {
        // Geçiş öncesi kayıt: SurveyInvitedAt boş, ResolvedAt dolu
        var t = new Ticket
        {
            Status = TicketStatus.Resolved,
            ResolvedAt = Now.AddDays(-2),
            SurveyInvitedAt = null
        };

        Assert.Equal(Now.AddDays(-2).AddDays(ExpiresAfterDays), SurveyPolicy.Deadline(t, ExpiresAfterDays));
        Assert.True(SurveyPolicy.CanSubmit(t, Now, ExpiresAfterDays));
    }

    [Fact]
    public void Doldurulmus_anket_tekrar_doldurulamaz()
    {
        var t = Resolved(hoursAgo: 48, survey: new SurveyResponse());

        Assert.False(SurveyPolicy.CanSubmit(t, Now, ExpiresAfterDays));
    }

    // ───── Hatırlatma ────────────────────────────────────────────────────

    [Fact]
    public void Gecikme_dolmadan_hatirlatma_gitmez()
    {
        var t = Resolved(hoursAgo: 5);

        Assert.False(SurveyPolicy.ReminderDue(t, Now, ReminderAfterHours, ExpiresAfterDays));
    }

    [Fact]
    public void Gecikme_dolunca_hatirlatma_gider()
    {
        var t = Resolved(hoursAgo: 25);

        Assert.True(SurveyPolicy.ReminderDue(t, Now, ReminderAfterHours, ExpiresAfterDays));
    }

    [Fact]
    public void Ayni_talebe_ikinci_hatirlatma_gitmez()
    {
        var t = Resolved(hoursAgo: 72, reminderSentAt: Now.AddHours(-48));

        Assert.False(SurveyPolicy.ReminderDue(t, Now, ReminderAfterHours, ExpiresAfterDays));
    }

    [Fact]
    public void Suresi_dolmus_ankete_hatirlatma_gitmez()
    {
        // Doldurulamayacak bir forma davet etmek güven kaybettirir
        var t = Resolved(hoursAgo: 20 * 24);

        Assert.True(SurveyPolicy.IsExpired(t, Now, ExpiresAfterDays));
        Assert.False(SurveyPolicy.ReminderDue(t, Now, ReminderAfterHours, ExpiresAfterDays));
    }

    [Fact]
    public void Cozulmemis_talebe_hatirlatma_gitmez()
    {
        var t = new Ticket { Status = TicketStatus.InProgress };

        Assert.False(SurveyPolicy.ReminderDue(t, Now, ReminderAfterHours, ExpiresAfterDays));
    }

    // ───── Kalan süre metni ──────────────────────────────────────────────

    [Theory]
    [InlineData(72, "3 gün")]
    [InlineData(5, "5 saat")]
    [InlineData(0.5, "30 dakika")]
    [InlineData(-1, "süre doldu")]
    public void Kalan_sure_metni(double hoursLeft, string expected)
    {
        Assert.Equal(expected, SurveyPolicy.RemainingText(Now.AddHours(hoursLeft), Now));
    }
}

// Anket penceresini açan / sıfırlayan yer: durum geçişi.
public class TicketWorkflowSurveyWindowTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Cozum_anket_penceresini_acar()
    {
        var t = new Ticket { Status = TicketStatus.InProgress };

        TicketWorkflow.Apply(t, TicketStatus.Resolved, Now);

        Assert.Equal(Now, t.SurveyInvitedAt);
        Assert.Null(t.SurveyReminderSentAt);
    }

    [Fact]
    public void Cozulmus_talebin_kapatilmasi_daveti_ileri_kaydirmaz()
    {
        var t = new Ticket { Status = TicketStatus.InProgress };
        TicketWorkflow.Apply(t, TicketStatus.Resolved, Now);

        TicketWorkflow.Apply(t, TicketStatus.Closed, Now.AddDays(3));

        // Süre çözümden sayılır: kapanış tarihi anketi uzatmaz
        Assert.Equal(Now, t.SurveyInvitedAt);
    }

    [Fact]
    public void Yeniden_acilis_pencereyi_ve_hatirlatmayi_sifirlar()
    {
        var t = new Ticket { Status = TicketStatus.InProgress };
        TicketWorkflow.Apply(t, TicketStatus.Resolved, Now);
        t.SurveyReminderSentAt = Now.AddHours(24);

        TicketWorkflow.Apply(t, TicketStatus.InProgress, Now.AddHours(30));

        Assert.Null(t.SurveyInvitedAt);
        Assert.Null(t.SurveyReminderSentAt);

        // İkinci çözümde pencere yeniden açılır ve hatırlatma yeniden gidebilir
        var ikinciCozum = Now.AddHours(40);
        TicketWorkflow.Apply(t, TicketStatus.Resolved, ikinciCozum);

        Assert.Equal(ikinciCozum, t.SurveyInvitedAt);
        Assert.True(SurveyPolicy.ReminderDue(t, ikinciCozum.AddHours(25),
            reminderAfterHours: 24, expiresAfterDays: 14));
    }
}
