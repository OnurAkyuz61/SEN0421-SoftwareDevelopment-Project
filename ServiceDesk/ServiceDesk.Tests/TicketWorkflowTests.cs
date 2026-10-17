using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Durum geçiş kuralları: onaysız değişiklik engeli, yeniden açılma sayacı
// ve çözüm/kapanış tarihlerinin doğru yönetilmesi.
public class TicketWorkflowTests
{
    private static readonly DateTime Simdi = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private static Ticket Ariza(TicketStatus durum = TicketStatus.New) => new()
    {
        Id = 1,
        Type = TicketType.Incident,
        Status = durum,
        ApprovalStatus = ApprovalStatus.NotRequired
    };

    private static Ticket Degisiklik(ApprovalStatus onay, TicketStatus durum = TicketStatus.New) => new()
    {
        Id = 2,
        Type = TicketType.Change,
        Status = durum,
        ApprovalStatus = onay
    };

    [Theory]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved)]
    public void Onaysiz_degisiklik_isleme_alinamaz(TicketStatus hedef)
    {
        var sonuc = TicketWorkflow.CanTransition(Degisiklik(ApprovalStatus.Pending), hedef);

        Assert.False(sonuc.Allowed);
        Assert.Contains("onaylanmadan", sonuc.Error);
    }

    [Fact]
    public void Onaylanmis_degisiklik_isleme_alinabilir()
    {
        var sonuc = TicketWorkflow.CanTransition(Degisiklik(ApprovalStatus.Approved), TicketStatus.InProgress);
        Assert.True(sonuc.Allowed);
    }

    [Fact]
    public void Reddedilmis_talep_yeniden_acilamaz()
    {
        var talep = Degisiklik(ApprovalStatus.Rejected, TicketStatus.Closed);
        var sonuc = TicketWorkflow.CanTransition(talep, TicketStatus.InProgress);

        Assert.False(sonuc.Allowed);
        Assert.Contains("yeniden açılamaz", sonuc.Error);
    }

    [Fact]
    public void Ariza_talebi_onay_gerektirmez()
    {
        var sonuc = TicketWorkflow.CanTransition(Ariza(), TicketStatus.Resolved);
        Assert.True(sonuc.Allowed);
    }

    [Fact]
    public void Cozulen_talep_cozum_tarihi_alir_kapanis_almaz()
    {
        var talep = Ariza(TicketStatus.InProgress);
        TicketWorkflow.Apply(talep, TicketStatus.Resolved, Simdi);

        Assert.Equal(Simdi, talep.ResolvedAt);
        Assert.Null(talep.ClosedAt);
    }

    [Fact]
    public void Kapanan_talep_hem_cozum_hem_kapanis_tarihi_alir()
    {
        var talep = Ariza(TicketStatus.InProgress);
        TicketWorkflow.Apply(talep, TicketStatus.Closed, Simdi);

        Assert.Equal(Simdi, talep.ResolvedAt);
        Assert.Equal(Simdi, talep.ClosedAt);
    }

    [Fact]
    public void Cozum_tarihi_bir_kez_yazilir_kapanista_degismez()
    {
        var cozum = Simdi.AddHours(-3);
        var talep = Ariza(TicketStatus.Resolved);
        talep.ResolvedAt = cozum;

        TicketWorkflow.Apply(talep, TicketStatus.Closed, Simdi);

        Assert.Equal(cozum, talep.ResolvedAt);   // ilk çözüm anı korunur
        Assert.Equal(Simdi, talep.ClosedAt);
    }

    [Fact]
    public void Yeniden_acilan_talepte_sayac_artar_tarihler_temizlenir()
    {
        var talep = Ariza(TicketStatus.Closed);
        talep.ResolvedAt = Simdi.AddHours(-5);
        talep.ClosedAt = Simdi.AddHours(-4);

        var karar = TicketWorkflow.CanTransition(talep, TicketStatus.InProgress);
        TicketWorkflow.Apply(talep, TicketStatus.InProgress, Simdi);

        Assert.True(karar.Reopened);
        Assert.Equal(1, talep.ReopenCount);
        Assert.Null(talep.ResolvedAt);
        Assert.Null(talep.ClosedAt);
    }

    [Fact]
    public void Acik_talepler_arasinda_gecis_sayaci_artirmaz()
    {
        var talep = Ariza(TicketStatus.New);
        TicketWorkflow.Apply(talep, TicketStatus.InProgress, Simdi);

        Assert.Equal(0, talep.ReopenCount);
        Assert.Null(talep.ResolvedAt);
    }

    [Fact]
    public void Ilk_yanit_yalnizca_bir_kez_isaretlenir()
    {
        var talep = Ariza(TicketStatus.InProgress);

        Assert.True(TicketWorkflow.MarkFirstResponse(talep, isInternalNote: false, Simdi));
        Assert.Equal(Simdi, talep.FirstRespondedAt);

        // İkinci yanıt ilk yanıt zamanını değiştirmez
        Assert.False(TicketWorkflow.MarkFirstResponse(talep, isInternalNote: false, Simdi.AddHours(2)));
        Assert.Equal(Simdi, talep.FirstRespondedAt);
    }

    [Fact]
    public void Ic_not_ilk_yanit_sayilmaz()
    {
        var talep = Ariza(TicketStatus.InProgress);

        Assert.False(TicketWorkflow.MarkFirstResponse(talep, isInternalNote: true, Simdi));
        Assert.Null(talep.FirstRespondedAt);
    }

    [Fact]
    public void Yuksek_riskli_degisiklik_iki_onay_ister()
    {
        var talep = Degisiklik(ApprovalStatus.Pending);
        talep.ChangeDetail = new ChangeDetail { TicketId = talep.Id, Risk = RiskLevel.High };

        Assert.Equal(2, talep.RequiredApprovalSteps);
    }

    [Theory]
    [InlineData(RiskLevel.Low)]
    [InlineData(RiskLevel.Medium)]
    public void Dusuk_ve_orta_riskli_degisiklik_tek_onay_ister(RiskLevel risk)
    {
        var talep = Degisiklik(ApprovalStatus.Pending);
        talep.ChangeDetail = new ChangeDetail { TicketId = talep.Id, Risk = risk };

        Assert.Equal(1, talep.RequiredApprovalSteps);
    }

    [Fact]
    public void Plani_olmayan_degisiklik_tek_onay_ister()
    {
        Assert.Equal(1, Degisiklik(ApprovalStatus.Pending).RequiredApprovalSteps);
    }
}
