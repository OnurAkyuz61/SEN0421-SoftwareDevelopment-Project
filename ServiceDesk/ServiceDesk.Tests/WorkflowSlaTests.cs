using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// "Müşteri bekleniyor" duraklaması, ilk yanıt ihlali, zorunlu çözüm kodu,
// round-robin sırası ve etiket kuralları
public class WorkflowSlaTests
{
    private static readonly DateTime Simdi = new(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc);

    private static BusinessCalendar Takvim() => new()
    {
        Id = 1, Name = "Test", WorkStartMinutes = 9 * 60, WorkEndMinutes = 18 * 60, WorkDays = "1,2,3,4,5"
    };

    private static Ticket Ariza(TicketStatus durum = TicketStatus.InProgress) => new()
    {
        Id = 1, Title = "Test", Description = "Test", Type = TicketType.Incident,
        Status = durum, ApprovalStatus = ApprovalStatus.NotRequired,
        CreatedAt = Simdi.AddHours(-2), SlaDueAt = Simdi.AddHours(6), FirstResponseDueAt = Simdi.AddHours(1)
    };

    [Fact]
    public void Mesai_dakikasi_hafta_sonunu_saymaz()
    {
        // Cuma 17:00 → Pazartesi 10:00 (yerel): 60 + 60 = 120 mesai dakikası
        var bas = AppTime.ToUtc(new DateTime(2026, 10, 23, 17, 0, 0));
        var bit = AppTime.ToUtc(new DateTime(2026, 10, 26, 10, 0, 0));
        Assert.Equal(120, SlaCalculator.WorkingMinutesBetween(bas, bit, Takvim()));
    }

    [Fact]
    public void Takvimsiz_mesai_dakikasi_takvim_saatidir()
    {
        Assert.Equal(90, SlaCalculator.WorkingMinutesBetween(Simdi, Simdi.AddMinutes(90), null));
        Assert.Equal(0, SlaCalculator.WorkingMinutesBetween(Simdi, Simdi.AddMinutes(-5), null));
    }

    [Fact]
    public void Musteri_bekleniyor_sla_sayacini_durdurur()
    {
        var talep = Ariza();
        Assert.True(TicketWorkflow.CanTransition(talep, TicketStatus.WaitingForCustomer).Allowed);

        TicketWorkflow.Apply(talep, TicketStatus.WaitingForCustomer, Simdi);
        Assert.Equal(Simdi, talep.SlaPausedAt);
        Assert.True(TicketWorkflow.IsOpen(talep.Status));
    }

    [Fact]
    public void Beklemeden_cikinca_hedef_bekleme_kadar_kayar()
    {
        var talep = Ariza();
        var hedef = talep.SlaDueAt;
        TicketWorkflow.Apply(talep, TicketStatus.WaitingForCustomer, Simdi);

        // Servis atlanırsa takvim saatiyle kaydırılır (yedek davranış)
        TicketWorkflow.Apply(talep, TicketStatus.InProgress, Simdi.AddMinutes(45));
        Assert.Null(talep.SlaPausedAt);
        Assert.Equal(45, talep.SlaPausedMinutes);
        Assert.Equal(hedef.AddMinutes(45), talep.SlaDueAt);
    }

    [Fact]
    public void Kapanmis_talep_beklemeye_alinamaz()
    {
        Assert.False(TicketWorkflow.CanTransition(Ariza(TicketStatus.Resolved), TicketStatus.WaitingForCustomer).Allowed);
    }

    [Fact]
    public void Onaysiz_degisiklik_beklemeye_alinamaz()
    {
        var talep = Ariza(TicketStatus.New);
        talep.Type = TicketType.Change;
        talep.ApprovalStatus = ApprovalStatus.Pending;
        Assert.False(TicketWorkflow.CanTransition(talep, TicketStatus.WaitingForCustomer).Allowed);
    }

    [Fact]
    public void Gec_ilk_yanit_ayri_ihlal_olarak_isaretlenir()
    {
        var talep = Ariza();
        TicketWorkflow.MarkFirstResponse(talep, false, talep.FirstResponseDueAt!.Value.AddMinutes(5));
        Assert.True(talep.IsFirstResponseBreached);
        Assert.False(talep.IsSlaBreached);
    }

    [Fact]
    public void Zamaninda_ilk_yanit_ihlal_degildir()
    {
        var talep = Ariza();
        TicketWorkflow.MarkFirstResponse(talep, false, Simdi);
        Assert.False(talep.IsFirstResponseBreached);
    }

    [Fact]
    public void Kapanista_cozum_kodu_ve_kok_neden_zorunlu()
    {
        var talep = Ariza();
        Assert.NotNull(TicketWorkflow.ValidateResolution(talep, TicketStatus.Resolved, null, "kök neden"));
        Assert.NotNull(TicketWorkflow.ValidateResolution(talep, TicketStatus.Closed, ResolutionCode.Fixed, " "));
        Assert.Null(TicketWorkflow.ValidateResolution(talep, TicketStatus.Resolved, ResolutionCode.Fixed, "Sürücü güncellendi"));
        Assert.Null(TicketWorkflow.ValidateResolution(talep, TicketStatus.WaitingForCustomer, null, null));
    }

    [Fact]
    public void Cozum_kodu_girilmis_talep_kapatilirken_tekrar_sorulmaz()
    {
        var talep = Ariza(TicketStatus.Resolved);
        talep.ResolutionCode = ResolutionCode.Workaround;
        Assert.Null(TicketWorkflow.ValidateResolution(talep, TicketStatus.Closed, null, null));
    }

    [Fact]
    public void Yeniden_acilan_talepte_cozum_kodu_temizlenir()
    {
        var talep = Ariza(TicketStatus.Resolved);
        talep.ResolutionCode = ResolutionCode.Fixed;
        talep.RootCause = "x";
        TicketWorkflow.Apply(talep, TicketStatus.InProgress, Simdi);
        Assert.Null(talep.ResolutionCode);
        Assert.Null(talep.RootCause);
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData(3, 7)]
    [InlineData(7, 9)]
    [InlineData(9, 3)]
    public void Round_robin_siradaki_kisiyi_secer(int? son, int beklenen)
    {
        Assert.Equal(beklenen, RoundRobinPolicy.Next(new[] { 3, 7, 9 }, son));
    }

    [Fact]
    public void Round_robin_ekip_bossa_atama_yapmaz()
    {
        Assert.Null(RoundRobinPolicy.Next(Array.Empty<int>(), null));
    }

    [Fact]
    public void Ekip_kotasi_sinirsizsa_hata_yok()
    {
        Assert.Null(TeamCapacityPolicy.Error("Ağ", null, 100));
        Assert.Null(TeamCapacityPolicy.Error("Ağ", 0, 100)); // 0 kayıtta null'a çevrilir; politika da sınır saymaz
    }

    [Fact]
    public void Ekip_kotasi_dolunca_reddedilir()
    {
        var hata = TeamCapacityPolicy.Error("Son Kullanıcı Desteği", 5, 5);
        Assert.Contains("kotası dolu", hata!);
        Assert.Contains("5/5", hata);
    }

    [Fact]
    public void Ekip_kotasi_yer_varken_kabul_edilir()
    {
        Assert.Null(TeamCapacityPolicy.Error("Ağ", 10, 9));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Ekip_kota_limiti_gecersiz_deger_reddedilir(int deger)
    {
        Assert.NotNull(TeamCapacityPolicy.ValidateLimit(deger));
    }

    [Fact]
    public void Ekip_kota_limiti_bos_veya_pozitif_kabul()
    {
        Assert.Null(TeamCapacityPolicy.ValidateLimit(null));
        Assert.Null(TeamCapacityPolicy.ValidateLimit(25));
    }

    [Fact]
    public void Etiketler_kucuk_harf_ve_tekrarsiz_saklanir()
    {
        var (deger, hata) = TicketTags.Parse(" VPN, laptop ,vpn,, ");
        Assert.Null(hata);
        Assert.Equal("vpn,laptop", deger);
        Assert.NotNull(TicketTags.Parse(string.Join(",", Enumerable.Range(1, 11).Select(i => $"e{i}"))).Error);
    }

    [Fact]
    public void Ekipli_destek_yalnizca_kendi_ekibini_gorur()
    {
        var destek = new AppUser { Id = 5, Role = UserRole.Agent, TeamId = 1 };
        var yonetici = new AppUser { Id = 6, Role = UserRole.Admin, TeamId = 1 };

        Assert.True(TicketScope.CanSee(destek, new Ticket { AssignedTeamId = 1 }));
        Assert.True(TicketScope.CanSee(destek, new Ticket { AssignedTeamId = 2, AssigneeId = 5 }));
        Assert.False(TicketScope.CanSee(destek, new Ticket { AssignedTeamId = 2 }));
        Assert.True(TicketScope.CanSee(yonetici, new Ticket { AssignedTeamId = 2 }));
    }
}
