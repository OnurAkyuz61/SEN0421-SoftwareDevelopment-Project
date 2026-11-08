using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Approvals;
using ServiceDesk.Services.Escalations;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Tests;

// Onay: vekalet, uygulama penceresi, CAB, tur, zincir kuralı, yeniden gönderim
public class ApprovalExtensionsTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc);

    private static AppUser U(UserRole r, int id) => new() { Id = id, FirstName = "T", LastName = id.ToString(), Role = r, IsActive = true };

    private static Ticket Change(RiskLevel risk = RiskLevel.Low, int requester = 100)
    {
        var t = new Ticket { Id = 9, Type = TicketType.Change, ApprovalStatus = ApprovalStatus.Pending, RequesterId = requester, CategoryId = 1, Priority = Priority.Medium };
        t.ChangeDetail = new ChangeDetail { TicketId = 9, Risk = risk };
        return t;
    }

    private static ApprovalContext Ctx(params AppUser[] delegators) =>
        new(Now, delegators, new ApprovalSettings());

    [Fact]
    public void Vekil_destek_yonetici_adina_nihai_onay_verir()
    {
        var admin = U(UserRole.Admin, 1);
        var sonuc = ApprovalPolicy.CanDecide(Change(), U(UserRole.Agent, 2), true, Ctx(admin));

        Assert.True(sonuc.Allowed);
        Assert.Equal(admin.Id, sonuc.OnBehalfOf?.Id);
    }

    [Fact]
    public void Vekaletsiz_destek_nihai_onay_veremez()
    {
        var sonuc = ApprovalPolicy.CanDecide(Change(), U(UserRole.Agent, 2), true, Ctx());
        Assert.False(sonuc.Allowed);
    }

    [Fact]
    public void Vekil_vekaleti_verenin_zaten_onayladigi_turda_onun_yerine_onay_veremez()
    {
        var admin = U(UserRole.Admin, 1);
        var t = Change(RiskLevel.High);
        t.Approvals.Add(new ChangeApproval { Round = 1, Step = 1, ApproverId = admin.Id, Decision = ApprovalStatus.Approved });
        t.ChangeDetail!.CabMeetingUrl = "https://cab";

        var sonuc = ApprovalPolicy.CanDecide(t, U(UserRole.Agent, 2), true, Ctx(admin));
        Assert.False(sonuc.Allowed);
    }

    [Fact]
    public void Pencere_gectiyse_onay_engellenir_red_serbest()
    {
        var t = Change();
        t.ChangeDetail!.PlannedStart = Now.AddDays(-2);
        t.ChangeDetail.PlannedEnd = Now.AddDays(-1);
        var admin = U(UserRole.Admin, 1);

        var onay = ApprovalPolicy.CanDecide(t, admin, true, Ctx());
        var red = ApprovalPolicy.CanDecide(t, admin, false, Ctx());

        Assert.False(onay.Allowed);
        Assert.Contains("penceresi geçti", onay.Error);
        Assert.True(red.Allowed);
    }

    [Fact]
    public void Pencere_zorunluysa_pencere_olmadan_onay_verilmez()
    {
        var ctx = new ApprovalContext(Now, Array.Empty<AppUser>(), new ApprovalSettings { RequirePlannedWindow = true });
        var sonuc = ApprovalPolicy.CanDecide(Change(), U(UserRole.Admin, 1), true, ctx);
        Assert.False(sonuc.Allowed);
    }

    [Fact]
    public void Yuksek_risk_nihai_onay_CAB_kaydi_ister()
    {
        var t = Change(RiskLevel.High);
        t.Approvals.Add(new ChangeApproval { Round = 1, Step = 1, ApproverId = 2, Decision = ApprovalStatus.Approved });
        var admin = U(UserRole.Admin, 1);

        Assert.Contains("CAB", ApprovalPolicy.CanDecide(t, admin, true, Ctx()).Error);

        t.ChangeDetail!.CabMeetingUrl = "https://teams.example/cab-42";
        Assert.True(ApprovalPolicy.CanDecide(t, admin, true, Ctx()).Allowed);
    }

    [Fact]
    public void Onceki_turun_onaylari_yeni_turda_sayilmaz()
    {
        var t = Change(RiskLevel.High);
        t.Approvals.Add(new ChangeApproval { Round = 1, Step = 1, ApproverId = 2, Decision = ApprovalStatus.Approved });
        t.ApprovalRound = 2;

        Assert.Equal(1, ApprovalPolicy.NextStep(t));
        Assert.False(TicketWorkflow.IsApprovalStarted(t));
    }

    [Fact]
    public void Zincir_kurali_adim_sayisini_artirir_ama_risk_varsayilaninin_altina_inmez()
    {
        var t = Change(RiskLevel.High);
        var kurallar = new[]
        {
            new ApprovalChainRule { CategoryId = 1, RequiredSteps = 3, IsActive = true },
            new ApprovalChainRule { CategoryId = 2, RequiredSteps = 5, IsActive = true },
            new ApprovalChainRule { MinPriority = Priority.Critical, RequiredSteps = 4, IsActive = true },
            new ApprovalChainRule { Risk = RiskLevel.High, RequiredSteps = 1, IsActive = true }
        };

        Assert.Equal(3, ApprovalChainPolicy.Resolve(kurallar, t));
        Assert.Null(ApprovalChainPolicy.Resolve(Array.Empty<ApprovalChainRule>(), t));

        t.ApprovalStepsRequired = 3;
        Assert.Equal(3, t.RequiredApprovalSteps);
    }

    [Fact]
    public void Reddedilen_talep_sinira_kadar_yeniden_gonderilebilir()
    {
        var t = Change();
        t.ApprovalStatus = ApprovalStatus.Rejected;
        var sahibi = U(UserRole.User, 100);

        Assert.True(ApprovalPolicy.CanResubmit(t, sahibi, 3).Allowed);
        Assert.False(ApprovalPolicy.CanResubmit(t, U(UserRole.User, 55), 3).Allowed);

        t.ApprovalRound = 4;
        Assert.False(ApprovalPolicy.CanResubmit(t, sahibi, 3).Allowed);
    }

    [Fact]
    public void Uygulama_durumu_gecisleri()
    {
        var t = Change();
        t.ApprovalStatus = ApprovalStatus.Approved;
        t.ChangeDetail!.PlannedStart = Now.AddHours(-1);
        t.ChangeDetail.PlannedEnd = Now.AddHours(2);

        Assert.True(ChangeImplementationRules.CanMove(t, ImplementationStatus.InProgress, Now).Allowed);
        Assert.False(ChangeImplementationRules.CanMove(t, ImplementationStatus.Implemented, Now).Allowed);
        Assert.False(ChangeImplementationRules.CanMove(t, ImplementationStatus.InProgress, Now.AddHours(5)).Allowed);

        ChangeImplementationRules.Apply(t.ChangeDetail, ImplementationStatus.InProgress, Now, null);
        Assert.True(ChangeImplementationRules.CanMove(t, ImplementationStatus.Failed, Now).Allowed);
        ChangeImplementationRules.Apply(t.ChangeDetail, ImplementationStatus.Failed, Now, "disk doldu");
        Assert.True(ChangeImplementationRules.CanMove(t, ImplementationStatus.RolledBack, Now).Allowed);
    }

    [Fact]
    public void Onay_hatirlatma_zamani()
    {
        Assert.False(ApprovalReminderService.IsDue(Now.AddHours(-10), null, Now, 24, 24));
        Assert.True(ApprovalReminderService.IsDue(Now.AddHours(-25), null, Now, 24, 24));
        Assert.False(ApprovalReminderService.IsDue(Now.AddHours(-50), Now.AddHours(-3), Now, 24, 24));
        Assert.True(ApprovalReminderService.IsDue(Now.AddHours(-50), Now.AddHours(-25), Now, 24, 24));
    }
}

public class EscalationAndNotificationTests
{
    [Theory]
    [InlineData(Priority.Low, Priority.Medium)]
    [InlineData(Priority.High, Priority.Critical)]
    [InlineData(Priority.Critical, Priority.Critical)]
    public void Eskalasyon_onceligi_bir_kademe_artirir(Priority once, Priority sonra) =>
        Assert.Equal(sonra, EscalationPolicy.Raise(once));

    [Fact]
    public void Gecikmeye_gore_uygulanacak_kurallar()
    {
        var rules = new[]
        {
            new EscalationRule { Level = 1, AfterMinutes = 0, IsActive = true },
            new EscalationRule { Level = 2, AfterMinutes = 120, IsActive = true },
            new EscalationRule { Level = 3, AfterMinutes = 60, IsActive = false }
        };

        Assert.Equal(new[] { 1 }, EscalationPolicy.DueRules(rules, 0, 30).Select(r => r.Level));
        Assert.Equal(new[] { 2 }, EscalationPolicy.DueRules(rules, 1, 130).Select(r => r.Level));
        Assert.Equal(2, EscalationPolicy.NextRule(rules, 1)?.Level);
        Assert.Null(EscalationPolicy.NextRule(rules, 2));
    }

    [Fact]
    public void Yeniden_deneme_ustel_geri_cekilme_ve_sinir()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(now.AddSeconds(60), DeliveryRetryPolicy.AfterFailure(1, now, 5, 60, 3600).NextAttemptAt);
        Assert.Equal(now.AddSeconds(240), DeliveryRetryPolicy.AfterFailure(3, now, 5, 60, 3600).NextAttemptAt);
        Assert.Equal(now.AddSeconds(300), DeliveryRetryPolicy.AfterFailure(4, now, 5, 60, 300).NextAttemptAt);
        Assert.True(DeliveryRetryPolicy.AfterFailure(5, now, 5, 60, 3600).GiveUp);
    }

    [Fact]
    public void Kanal_secimi_tercih_kilit_ve_iletisim_bilgisine_uyar()
    {
        // Varsayılan: SLA ihlali uygulama içi + e-posta + SMS
        var d = NotificationChannelPolicy.Resolve(NotificationType.SlaBreach, null, hasEmail: true, hasPhone: true);
        Assert.True(d.InApp && d.Email && d.Sms);

        // Telefon yoksa SMS düşer
        Assert.False(NotificationChannelPolicy.Resolve(NotificationType.SlaBreach, null, true, false).Sms);

        // Kilitli türde uygulama içi kapatılamaz
        var pref = new NotificationPreference { Type = NotificationType.Escalation, InApp = false, Email = false, Sms = false };
        var r = NotificationChannelPolicy.Resolve(NotificationType.Escalation, pref, true, true);
        Assert.True(r.InApp);
        Assert.False(r.Email);

        // SMS'e izin verilmeyen türde tercih açık olsa da SMS gitmez
        var yorum = new NotificationPreference { Type = NotificationType.NewComment, InApp = true, Email = true, Sms = true };
        Assert.False(NotificationChannelPolicy.Resolve(NotificationType.NewComment, yorum, true, true).Sms);
    }

    [Fact]
    public void Eskalasyon_seviyesine_gore_farkli_sablon()
    {
        Assert.Equal("escalation.l1", EmailTemplates.Escalation(1));
        Assert.Equal("escalation.l2", EmailTemplates.Escalation(2));
        Assert.Equal("escalation.l3", EmailTemplates.Escalation(7));

        var m = new EmailTemplates.Model("Ayşe", "Eskalasyon (seviye 2): INC-1 Yazıcı", "Gecikme", "INC-1", "https://x/Tickets/Details/1");
        var l1 = EmailTemplates.Render(EmailTemplates.Escalation(1), m);
        var l3 = EmailTemplates.Render(EmailTemplates.Escalation(3), m);
        Assert.NotEqual(l1.Subject, l3.Subject);
        Assert.Contains("KRİTİK", l3.Subject);
        Assert.Contains("https://x/Tickets/Details/1", l1.Html);

        // Atama, yorum ve SLA şablonları birbirinden ayrı
        var konular = new[] { EmailTemplates.Assignment, EmailTemplates.Comment, EmailTemplates.SlaBreach }
            .Select(k => EmailTemplates.Render(k, m).Subject).Distinct().Count();
        Assert.Equal(3, konular);
    }

    [Fact]
    public void Sablon_html_kodlar()
    {
        var m = new EmailTemplates.Model("<b>x</b>", "<script>", "a & b", null, null);
        var r = EmailTemplates.Render(EmailTemplates.Comment, m);
        Assert.DoesNotContain("<script>", r.Html);
        Assert.Contains("&lt;script&gt;", r.Html);
    }

    [Fact]
    public void Gunluk_ozet_saati_gelmeden_calismaz()
    {
        // AppTime yerel saati Türkiye (UTC+3) kabul eder: 04:00 UTC = 07:00 yerel
        var erken = new DateTime(2026, 11, 8, 4, 0, 0, DateTimeKind.Utc);
        var gec = new DateTime(2026, 11, 8, 6, 0, 0, DateTimeKind.Utc);
        Assert.Null(DailyDigestService.TodaysSlotUtc(erken, 8));
        Assert.NotNull(DailyDigestService.TodaysSlotUtc(gec, 8));
    }
}
