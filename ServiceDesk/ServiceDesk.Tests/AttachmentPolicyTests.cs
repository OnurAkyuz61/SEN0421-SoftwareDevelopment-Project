using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Ek dosyayı kim açabilir: /Attachments/Get/{id} adresi doğrudan denenerek
// başkasının dosyası ya da kendi talebindeki iç not eki indirilememeli.
public class AttachmentPolicyTests
{
    private const int SahibiId = 50;
    private const int BaskasiId = 51;

    private static AppUser Kullanici(UserRole rol, int id) =>
        new() { Id = id, FirstName = "Test", LastName = "Kisi", Role = rol };

    private static Attachment Ek(bool icNot = false, int talepSahibi = SahibiId,
                                TicketStatus durum = TicketStatus.InProgress,
                                int? yukleyen = SahibiId, bool arsiv = false,
                                bool silinmis = false,
                                AttachmentScanStatus tarama = AttachmentScanStatus.Clean)
    {
        var talep = new Ticket { Id = 11, RequesterId = talepSahibi, Status = durum, IsDeleted = arsiv };
        return new Attachment
        {
            Id = 3,
            TicketId = talep.Id,
            Ticket = talep,
            FileName = "ekran.png",
            StoredFileName = "abc.png",
            ContentType = "image/png",
            UploadedById = yukleyen,
            IsDeleted = silinmis,
            ScanStatus = tarama,
            Comment = icNot ? new TicketComment { Id = 4, TicketId = talep.Id, IsInternal = true } : null,
            CommentId = icNot ? 4 : null
        };
    }

    [Fact]
    public void Talep_sahibi_kendi_ekini_acabilir()
    {
        Assert.True(AttachmentPolicy.CanView(Ek(), Kullanici(UserRole.User, SahibiId)));
    }

    [Fact]
    public void Baskasinin_talebindeki_ek_acilamaz()
    {
        Assert.False(AttachmentPolicy.CanView(Ek(), Kullanici(UserRole.User, BaskasiId)));
    }

    [Fact]
    public void Talep_sahibi_ic_nota_eklenen_dosyayi_acamaz()
    {
        // İç notlar portalda hiç gösterilmiyor; eki de gösterilmemeli
        Assert.False(AttachmentPolicy.CanView(Ek(icNot: true), Kullanici(UserRole.User, SahibiId)));
    }

    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public void Destek_ve_yonetici_ic_not_ekine_de_erisir(UserRole rol)
    {
        Assert.True(AttachmentPolicy.CanView(Ek(icNot: true), Kullanici(rol, 9)));
    }

    // ───── Kaldırma (yumuşak silme) ──────────────────────────────────────

    [Fact]
    public void Kullanici_kendi_yukledigi_eki_kaldirabilir()
    {
        Assert.True(AttachmentPolicy.CanDelete(Ek(), Kullanici(UserRole.User, SahibiId)));
    }

    [Fact]
    public void Kullanici_baskasinin_yukledigi_eki_kaldiramaz()
    {
        // Destek personelinin kendi talebine yüklediği dosya kullanıcıya ait değil
        var ek = Ek(yukleyen: 9);
        Assert.False(AttachmentPolicy.CanDelete(ek, Kullanici(UserRole.User, SahibiId)));
    }

    [Fact]
    public void Kullanici_kapanmis_talebin_ekini_kaldiramaz()
    {
        // Kapanan talebin ekleri kayıttır; kullanıcı sonradan temizleyemez
        var ek = Ek(durum: TicketStatus.Closed);
        Assert.False(AttachmentPolicy.CanDelete(ek, Kullanici(UserRole.User, SahibiId)));
    }

    [Fact]
    public void Baskasinin_talebindeki_ek_kaldirilamaz()
    {
        Assert.False(AttachmentPolicy.CanDelete(Ek(), Kullanici(UserRole.User, BaskasiId)));
    }

    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public void Destek_ve_yonetici_baskasinin_ekini_kaldirabilir(UserRole rol)
    {
        // Yanlış yüklenen / kişisel veri içeren dosyayı temizlemek onların işi
        Assert.True(AttachmentPolicy.CanDelete(Ek(yukleyen: SahibiId), Kullanici(rol, 9)));
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public void Arsivlenmis_talebin_eki_kaldirilamaz(UserRole rol)
    {
        // Arşiv denetim kaydıdır (ISO 9001 md. 7.5): içeriği dondurulur
        var ek = Ek(arsiv: true);
        Assert.False(AttachmentPolicy.CanDelete(ek, Kullanici(rol, rol == UserRole.User ? SahibiId : 9)));
    }

    [Fact]
    public void Zaten_kaldirilmis_ek_yeniden_kaldirilamaz()
    {
        var ek = Ek(silinmis: true);
        Assert.False(AttachmentPolicy.CanDelete(ek, Kullanici(UserRole.Admin, 9)));
        Assert.False(AttachmentPolicy.CanDelete(ek, Kullanici(UserRole.User, SahibiId)));
    }

    [Fact]
    public void Ic_nota_eklenen_dosya_kullanici_tarafindan_kaldirilamaz()
    {
        // Göremediği dosyayı silemez de
        Assert.False(AttachmentPolicy.CanDelete(Ek(icNot: true), Kullanici(UserRole.User, SahibiId)));
    }

    // ───── Tarama kapısı ─────────────────────────────────────────────────

    [Theory]
    [InlineData(AttachmentScanStatus.Clean, true)]
    [InlineData(AttachmentScanStatus.Skipped, true)]
    [InlineData(AttachmentScanStatus.Pending, false)]
    [InlineData(AttachmentScanStatus.Suspicious, false)]
    [InlineData(AttachmentScanStatus.Failed, false)]
    public void Yalnizca_taramasi_temiz_dosya_servis_edilir(AttachmentScanStatus durum, bool beklenen)
    {
        Assert.Equal(beklenen, AttachmentPolicy.IsServable(Ek(tarama: durum)));
    }

    [Fact]
    public void Tarama_yetkiden_bagimsizdir()
    {
        // Şüpheli dosyayı yönetici de indiremez, ama kaldırabilir
        var ek = Ek(tarama: AttachmentScanStatus.Suspicious);
        var yonetici = Kullanici(UserRole.Admin, 9);

        Assert.True(AttachmentPolicy.CanView(ek, yonetici));
        Assert.False(AttachmentPolicy.IsServable(ek));
        Assert.True(AttachmentPolicy.CanDelete(ek, yonetici));
    }

    [Fact]
    public void Talebi_yuklenmemis_ek_acilamaz()
    {
        // Arşivlenmiş talebin eki sorguya düşmez: Ticket boş kalır
        var ek = new Attachment { Id = 3, TicketId = 11, FileName = "a.pdf", StoredFileName = "a.pdf" };

        Assert.False(AttachmentPolicy.CanView(ek, Kullanici(UserRole.User, SahibiId)));
        Assert.False(AttachmentPolicy.CanView(ek, Kullanici(UserRole.Admin, 9)));

        // Görme hakkı olmayan kayıt silinemez de (NullReferenceException atmaz)
        Assert.False(AttachmentPolicy.CanDelete(ek, Kullanici(UserRole.Admin, 9)));
    }
}
