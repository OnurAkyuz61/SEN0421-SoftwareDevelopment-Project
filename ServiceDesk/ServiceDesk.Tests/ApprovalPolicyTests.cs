using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Değişiklik onayında yetki kuralları: hangi rol neyi yapabilir, kimse kendi
// talebini karara bağlayabilir mi, zincir tek kişiyle tamamlanabilir mi.
public class ApprovalPolicyTests
{
    private const int TalepSahibiId = 100;

    private static AppUser Kullanici(UserRole rol, int id) =>
        new() { Id = id, FirstName = "Test", LastName = "Kisi", Role = rol };

    private static AppUser Yonetici(int id = 1) => Kullanici(UserRole.Admin, id);
    private static AppUser Destek(int id = 2) => Kullanici(UserRole.Agent, id);
    private static AppUser SonKullanici(int id = 3) => Kullanici(UserRole.User, id);

    private static Ticket Degisiklik(RiskLevel risk = RiskLevel.Low)
    {
        var t = new Ticket
        {
            Id = 7,
            Number = "CHG-2026-00007",
            Type = TicketType.Change,
            Status = TicketStatus.New,
            ApprovalStatus = ApprovalStatus.Pending,
            RequesterId = TalepSahibiId
        };
        t.ChangeDetail = new ChangeDetail { TicketId = t.Id, Risk = risk };
        return t;
    }

    [Fact]
    public void Yonetici_tek_adimli_degisikligi_onaylar()
    {
        var sonuc = ApprovalPolicy.CanDecide(Degisiklik(), Yonetici(), approve: true);
        Assert.True(sonuc.Allowed);
    }

    [Fact]
    public void Destek_nihai_onayi_veremez()
    {
        var sonuc = ApprovalPolicy.CanDecide(Degisiklik(), Destek(), approve: true);

        Assert.False(sonuc.Allowed);
        Assert.Contains("nihai onayını", sonuc.Error);
    }

    [Fact]
    public void Destek_yuksek_riskli_degisikligin_ilk_onayini_verebilir()
    {
        var talep = Degisiklik(RiskLevel.High);   // iki adım ister

        var sonuc = ApprovalPolicy.CanDecide(talep, Destek(), approve: true);

        Assert.True(sonuc.Allowed);
        Assert.False(ApprovalPolicy.IsFinalStep(talep));
    }

    [Fact]
    public void Yuksek_riskli_degisikligin_ikinci_onayi_destekte_degil()
    {
        var talep = Degisiklik(RiskLevel.High);
        talep.Approvals.Add(new ChangeApproval
        {
            TicketId = talep.Id, Step = 1, ApproverId = 2, Decision = ApprovalStatus.Approved
        });

        // Başka bir destek personeli bile ikinci (nihai) onayı veremez
        var sonuc = ApprovalPolicy.CanDecide(talep, Destek(id: 9), approve: true);

        Assert.True(ApprovalPolicy.IsFinalStep(talep));
        Assert.False(sonuc.Allowed);
        Assert.Contains("yönetici", sonuc.Error);
    }

    [Fact]
    public void Ayni_kisi_onay_zincirini_tek_basina_tamamlayamaz()
    {
        var talep = Degisiklik(RiskLevel.High);
        var ben = Yonetici(id: 5);
        talep.Approvals.Add(new ChangeApproval
        {
            TicketId = talep.Id, Step = 1, ApproverId = ben.Id, Decision = ApprovalStatus.Approved
        });

        var sonuc = ApprovalPolicy.CanDecide(talep, ben, approve: true);

        Assert.False(sonuc.Allowed);
        Assert.Contains("zaten onayladınız", sonuc.Error);
    }

    [Fact]
    public void Destek_degisikligi_reddedemez()
    {
        var sonuc = ApprovalPolicy.CanDecide(Degisiklik(RiskLevel.High), Destek(), approve: false);

        Assert.False(sonuc.Allowed);
        Assert.Contains("reddedebilir", sonuc.Error);
    }

    [Fact]
    public void Yonetici_degisikligi_reddeder()
    {
        var sonuc = ApprovalPolicy.CanDecide(Degisiklik(), Yonetici(), approve: false);
        Assert.True(sonuc.Allowed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Son_kullanici_onay_akisinin_disinda(bool onay)
    {
        var sonuc = ApprovalPolicy.CanDecide(Degisiklik(), SonKullanici(), approve: onay);

        Assert.False(sonuc.Allowed);
        Assert.Contains("destek ve yönetici", sonuc.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Kendi_actigi_degisikligi_yonetici_de_karara_baglayamaz(bool onay)
    {
        // Görevlerin ayrılığı: talebi açan kişi aynı zamanda onaylayan olamaz
        var talepSahibiYonetici = Yonetici(id: TalepSahibiId);

        var sonuc = ApprovalPolicy.CanDecide(Degisiklik(), talepSahibiYonetici, approve: onay);

        Assert.False(sonuc.Allowed);
        Assert.Contains("Kendi açtığınız", sonuc.Error);
    }

    [Fact]
    public void Ariza_talebinde_onay_akisi_islemez()
    {
        var ariza = new Ticket
        {
            Id = 8, Type = TicketType.Incident,
            ApprovalStatus = ApprovalStatus.Pending, RequesterId = TalepSahibiId
        };

        var sonuc = ApprovalPolicy.CanDecide(ariza, Yonetici(), approve: true);

        Assert.False(sonuc.Allowed);
        Assert.Contains("değişiklik taleplerinde", sonuc.Error);
    }

    [Theory]
    [InlineData(ApprovalStatus.Approved)]
    [InlineData(ApprovalStatus.Rejected)]
    public void Karara_baglanmis_talep_tekrar_karara_baglanamaz(ApprovalStatus durum)
    {
        var talep = Degisiklik();
        talep.ApprovalStatus = durum;

        var sonuc = ApprovalPolicy.CanDecide(talep, Yonetici(), approve: true);

        Assert.False(sonuc.Allowed);
        Assert.Contains("zaten karara bağlanmış", sonuc.Error);
    }
}
