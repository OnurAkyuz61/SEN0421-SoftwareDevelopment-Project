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

    private static Attachment Ek(bool icNot = false, int talepSahibi = SahibiId)
    {
        var talep = new Ticket { Id = 11, RequesterId = talepSahibi };
        return new Attachment
        {
            Id = 3,
            TicketId = talep.Id,
            Ticket = talep,
            FileName = "ekran.png",
            StoredFileName = "abc.png",
            ContentType = "image/png",
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

    [Fact]
    public void Talebi_yuklenmemis_ek_acilamaz()
    {
        // Arşivlenmiş talebin eki sorguya düşmez: Ticket boş kalır
        var ek = new Attachment { Id = 3, TicketId = 11, FileName = "a.pdf", StoredFileName = "a.pdf" };

        Assert.False(AttachmentPolicy.CanView(ek, Kullanici(UserRole.User, SahibiId)));
        Assert.False(AttachmentPolicy.CanView(ek, Kullanici(UserRole.Admin, 9)));
    }
}
