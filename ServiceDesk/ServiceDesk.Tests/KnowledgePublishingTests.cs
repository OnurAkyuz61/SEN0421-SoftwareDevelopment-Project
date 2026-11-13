using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Bilgi bankası yayın yönetişimi: makaleyi kim görür, kim onaya gönderir,
// kim onaylar; kaydetmek ve sürüme geri dönmek durumu nasıl etkiler.
public class KnowledgePublishingTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);

    private const int YazarId = 10;
    private const int DigerAgentId = 11;
    private const int AdminId = 20;
    private const int DigerAdminId = 21;

    private static AppUser U(UserRole role, int id) =>
        new() { Id = id, FirstName = "T", LastName = id.ToString(), Role = role, IsActive = true };

    private static AppUser Yazar => U(UserRole.Agent, YazarId);
    private static AppUser BaskaAgent => U(UserRole.Agent, DigerAgentId);
    private static AppUser Admin => U(UserRole.Admin, AdminId);
    private static AppUser SonKullanici => U(UserRole.User, 30);

    private static KnowledgeArticle Makale(KnowledgeArticleStatus durum, int authorId = YazarId) =>
        new()
        {
            Id = 1,
            Title = "Yazıcı kağıt sıkışması",
            Body = "1. Yazıcıyı kapatın.",
            CategoryId = 2,
            AuthorId = authorId,
            Status = durum
        };

    // ───── Görünürlük ────────────────────────────────────────────────────

    [Theory]
    [InlineData(KnowledgeArticleStatus.Draft, false)]
    [InlineData(KnowledgeArticleStatus.PendingApproval, false)]
    [InlineData(KnowledgeArticleStatus.Rejected, false)]
    [InlineData(KnowledgeArticleStatus.Published, true)]
    public void Son_kullanici_yalnizca_yayindaki_makaleyi_gorur(KnowledgeArticleStatus durum, bool gorur) =>
        Assert.Equal(gorur, KnowledgePublishing.CanView(Makale(durum), SonKullanici));

    [Fact]
    public void Oturum_acmamis_ziyaretci_de_yalnizca_yayindakini_gorur()
    {
        Assert.False(KnowledgePublishing.CanView(Makale(KnowledgeArticleStatus.Draft), null));
        Assert.True(KnowledgePublishing.CanView(Makale(KnowledgeArticleStatus.Published), null));
    }

    [Fact]
    public void Destek_personeli_kendi_taslagini_gorur_baskasinin_taslagini_gormez()
    {
        var taslak = Makale(KnowledgeArticleStatus.Draft);

        Assert.True(KnowledgePublishing.CanView(taslak, Yazar));
        Assert.False(KnowledgePublishing.CanView(taslak, BaskaAgent));
    }

    [Fact]
    public void Yonetici_onay_kuyrugunu_gorebilmek_icin_hepsini_gorur()
    {
        foreach (var durum in Enum.GetValues<KnowledgeArticleStatus>())
            Assert.True(KnowledgePublishing.CanView(Makale(durum), Admin));
    }

    // Sorgu tarafı (VisibleTo) ile nesne tarafı (CanView) aynı sonucu
    // vermeli: görünürlük iki yerde ayrı ayrı yazılı olduğundan birbirinden
    // ayrı düşerse son kullanıcı listede taslak görür.
    [Fact]
    public void Sorgu_suzgeci_ile_nesne_kurali_ayni_sonucu_verir()
    {
        var makaleler = new List<KnowledgeArticle>();
        var id = 1;
        foreach (var durum in Enum.GetValues<KnowledgeArticleStatus>())
            foreach (var yazarId in new[] { YazarId, DigerAgentId })
                makaleler.Add(new KnowledgeArticle { Id = id++, AuthorId = yazarId, Status = durum });

        foreach (var kullanici in new[] { SonKullanici, Yazar, BaskaAgent, Admin })
        {
            var sorgudan = KnowledgePublishing.VisibleTo(makaleler.AsQueryable(), kullanici)
                .Select(a => a.Id).OrderBy(x => x).ToList();
            var kuraldan = makaleler.Where(a => KnowledgePublishing.CanView(a, kullanici))
                .Select(a => a.Id).OrderBy(x => x).ToList();

            Assert.Equal(kuraldan, sorgudan);
        }
    }

    // ───── Onaya gönderme ────────────────────────────────────────────────

    [Theory]
    [InlineData(KnowledgeArticleStatus.Draft, true)]
    [InlineData(KnowledgeArticleStatus.Rejected, true)]
    [InlineData(KnowledgeArticleStatus.PendingApproval, false)]
    [InlineData(KnowledgeArticleStatus.Published, false)]
    public void Onaya_yalnizca_taslak_ve_reddedilen_gonderilir(KnowledgeArticleStatus durum, bool olur) =>
        Assert.Equal(olur, KnowledgePublishing.CanSubmit(Makale(durum), Yazar).Allowed);

    [Fact]
    public void Baska_personelin_taslagi_onaya_gonderilemez() =>
        Assert.False(KnowledgePublishing.CanSubmit(Makale(KnowledgeArticleStatus.Draft), BaskaAgent).Allowed);

    [Fact]
    public void Son_kullanici_onaya_gonderemez() =>
        Assert.False(KnowledgePublishing.CanSubmit(Makale(KnowledgeArticleStatus.Draft), SonKullanici).Allowed);

    [Fact]
    public void Onaya_gonderim_kaydi_tutulur_ve_eski_red_gerekcesi_silinir()
    {
        var m = Makale(KnowledgeArticleStatus.Rejected);
        m.RejectionNote = "Adımlar eksik";

        KnowledgePublishing.Submit(m, Yazar, Now);

        Assert.Equal(KnowledgeArticleStatus.PendingApproval, m.Status);
        Assert.Equal(Now, m.SubmittedAt);
        Assert.Equal(YazarId, m.SubmittedById);
        Assert.Null(m.RejectionNote);
    }

    // ───── Onay / red ────────────────────────────────────────────────────

    [Fact]
    public void Onay_yalnizca_yoneticide()
    {
        var m = Makale(KnowledgeArticleStatus.PendingApproval);

        Assert.False(KnowledgePublishing.CanDecide(m, Yazar).Allowed);
        Assert.False(KnowledgePublishing.CanDecide(m, SonKullanici).Allowed);
        Assert.True(KnowledgePublishing.CanDecide(m, Admin).Allowed);
    }

    [Fact]
    public void Onay_bekleyen_olmayan_makale_onaylanamaz()
    {
        Assert.False(KnowledgePublishing.CanDecide(Makale(KnowledgeArticleStatus.Draft), Admin).Allowed);
        Assert.False(KnowledgePublishing.CanDecide(Makale(KnowledgeArticleStatus.Published), Admin).Allowed);
    }

    // md. 7.5.2: gözden geçirme ikinci bir göz demektir
    [Fact]
    public void Yonetici_kendi_yazdigi_makaleyi_onaylayamaz()
    {
        var kendi = Makale(KnowledgeArticleStatus.PendingApproval, authorId: AdminId);

        Assert.False(KnowledgePublishing.CanDecide(kendi, Admin).Allowed);
        Assert.True(KnowledgePublishing.CanDecide(kendi, U(UserRole.Admin, DigerAdminId)).Allowed);
    }

    [Fact]
    public void Onaylanan_makale_yayina_cikar_ve_onay_izi_kalir()
    {
        var m = Makale(KnowledgeArticleStatus.PendingApproval);

        KnowledgePublishing.Approve(m, Admin, Now);

        Assert.Equal(KnowledgeArticleStatus.Published, m.Status);
        Assert.Equal(Now, m.ApprovedAt);
        Assert.Equal(AdminId, m.ApprovedById);
        Assert.True(KnowledgePublishing.IsPublic(m));
    }

    [Fact]
    public void Red_gerekcesiz_reddedilemez()
    {
        var m = Makale(KnowledgeArticleStatus.PendingApproval);

        Assert.False(KnowledgePublishing.CanReject(m, Admin, null).Allowed);
        Assert.False(KnowledgePublishing.CanReject(m, Admin, "   ").Allowed);
        Assert.True(KnowledgePublishing.CanReject(m, Admin, "Adımlar eksik").Allowed);
    }

    [Fact]
    public void Reddedilen_makale_yayina_cikmaz_gerekce_yazara_kalir()
    {
        var m = Makale(KnowledgeArticleStatus.PendingApproval);

        KnowledgePublishing.Reject(m, Admin, "  Adımlar eksik  ", Now);

        Assert.Equal(KnowledgeArticleStatus.Rejected, m.Status);
        Assert.Equal("Adımlar eksik", m.RejectionNote);
        Assert.False(KnowledgePublishing.IsPublic(m));
    }

    // ───── Kaydetmenin durum üzerindeki etkisi ───────────────────────────

    [Fact]
    public void Icerik_degismediyse_durum_degismez()
    {
        foreach (var durum in Enum.GetValues<KnowledgeArticleStatus>())
            Assert.Equal(durum, KnowledgePublishing.StatusAfterEdit(durum, contentChanged: false));
    }

    // md. 7.5.3.2: yayımlanmış bilginin değişikliği de kontrol edilmeli
    [Fact]
    public void Yayindaki_makalenin_icerigi_degisirse_onaya_geri_doner()
    {
        var m = Makale(KnowledgeArticleStatus.Published);
        m.ApprovedAt = Now.AddDays(-3);
        m.ApprovedById = AdminId;

        KnowledgePublishing.ApplyEdit(m, Yazar, contentChanged: true, nowUtc: Now);

        Assert.Equal(KnowledgeArticleStatus.PendingApproval, m.Status);
        Assert.False(KnowledgePublishing.IsPublic(m));
        // Yeni metni kimse onaylamadı: eski onay kaydı silinir
        Assert.Null(m.ApprovedAt);
        Assert.Null(m.ApprovedById);
        // Değişikliği yapan kişi aynı zamanda onaya gönderendir
        Assert.Equal(Now, m.SubmittedAt);
        Assert.Equal(YazarId, m.SubmittedById);
    }

    [Fact]
    public void Onay_bekleyen_makale_duzenlenirse_kuyruktan_cikar()
    {
        var m = Makale(KnowledgeArticleStatus.PendingApproval);
        m.SubmittedAt = Now.AddHours(-2);
        m.SubmittedById = YazarId;

        KnowledgePublishing.ApplyEdit(m, Yazar, contentChanged: true, nowUtc: Now);

        Assert.Equal(KnowledgeArticleStatus.Draft, m.Status);
        Assert.Null(m.SubmittedAt);
        Assert.Null(m.SubmittedById);
    }

    [Fact]
    public void Reddedilen_makale_duzenlenince_taslaga_doner_gerekce_dusar()
    {
        var m = Makale(KnowledgeArticleStatus.Rejected);
        m.RejectionNote = "Adımlar eksik";

        KnowledgePublishing.ApplyEdit(m, Yazar, contentChanged: true, nowUtc: Now);

        Assert.Equal(KnowledgeArticleStatus.Draft, m.Status);
        Assert.Null(m.RejectionNote);
    }

    [Fact]
    public void Taslak_kaydedilince_taslak_kalir() =>
        Assert.Equal(KnowledgeArticleStatus.Draft,
            KnowledgePublishing.StatusAfterEdit(KnowledgeArticleStatus.Draft, contentChanged: true));

    // ───── Sürüm geçmişi ─────────────────────────────────────────────────

    [Fact]
    public void Surum_numarasi_birden_baslar_ve_birer_artar()
    {
        Assert.Equal(1, KnowledgePublishing.NextVersionNo(Array.Empty<KnowledgeArticleRevision>()));
        Assert.Equal(1, KnowledgePublishing.NextVersionNo((int?)null));

        var gecmis = new List<KnowledgeArticleRevision>
        {
            new() { VersionNo = 1 },
            new() { VersionNo = 2 }
        };
        Assert.Equal(3, KnowledgePublishing.NextVersionNo(gecmis));
        Assert.Equal(3, KnowledgePublishing.NextVersionNo(2));
    }

    [Fact]
    public void Surum_makalenin_o_anki_halini_kopyalar()
    {
        var m = Makale(KnowledgeArticleStatus.Published);

        var s = KnowledgePublishing.Snapshot(m, 4, YazarId, Now, "not");

        Assert.Equal(m.Id, s.ArticleId);
        Assert.Equal(4, s.VersionNo);
        Assert.Equal(m.Title, s.Title);
        Assert.Equal(m.Body, s.Body);
        Assert.Equal(m.CategoryId, s.CategoryId);
        Assert.Equal(YazarId, s.CreatedById);
        Assert.Equal(Now, s.CreatedAt);

        // Anlık görüntü: makale sonradan değişse sürüm metni değişmez
        m.Title = "Başka başlık";
        Assert.Equal("Yazıcı kağıt sıkışması", s.Title);
    }

    [Fact]
    public void Ayni_icerikli_surum_fark_olarak_sayilmaz()
    {
        var m = Makale(KnowledgeArticleStatus.Published);
        var s = KnowledgePublishing.Snapshot(m, 1, YazarId, Now);

        Assert.False(KnowledgePublishing.DiffersFrom(s, m));

        m.Body = "1. Yazıcıyı kapatın.\n2. Arka kapağı açın.";
        Assert.True(KnowledgePublishing.DiffersFrom(s, m));

        // Kategori de sürümün parçası: yalnızca sınıflandırma değişse bile fark var
        var s2 = KnowledgePublishing.Snapshot(m, 2, YazarId, Now);
        m.CategoryId = 5;
        Assert.True(KnowledgePublishing.DiffersFrom(s2, m));
    }

    [Fact]
    public void Surume_geri_donus_icerigi_yazar_ve_makaleyi_yeniden_onaya_dusurur()
    {
        var m = Makale(KnowledgeArticleStatus.Published);
        var ilk = KnowledgePublishing.Snapshot(m, 1, YazarId, Now.AddDays(-5));

        // Makale sonradan değişti
        m.Title = "Yeni başlık";
        m.Body = "Yeni içerik";
        m.CategoryId = 6;

        KnowledgePublishing.Restore(m, ilk);
        KnowledgePublishing.ApplyEdit(m, Yazar, contentChanged: true, nowUtc: Now);

        Assert.Equal("Yazıcı kağıt sıkışması", m.Title);
        Assert.Equal("1. Yazıcıyı kapatın.", m.Body);
        Assert.Equal(2, m.CategoryId);
        // Eski metin de onaydan geçmeden yayına dönmez
        Assert.Equal(KnowledgeArticleStatus.PendingApproval, m.Status);
    }

    // ───── Düzenleme yetkisi ─────────────────────────────────────────────

    [Fact]
    public void Yayindaki_makaleyi_butun_destek_ekibi_guncelleyebilir()
    {
        var yayinda = Makale(KnowledgeArticleStatus.Published);

        Assert.True(KnowledgePublishing.CanEdit(yayinda, BaskaAgent).Allowed);
        Assert.True(KnowledgePublishing.CanEdit(yayinda, Admin).Allowed);
        Assert.False(KnowledgePublishing.CanEdit(yayinda, SonKullanici).Allowed);
    }

    [Fact]
    public void Baska_personelin_taslagi_duzenlenemez()
    {
        var taslak = Makale(KnowledgeArticleStatus.Draft);

        Assert.True(KnowledgePublishing.CanEdit(taslak, Yazar).Allowed);
        Assert.True(KnowledgePublishing.CanEdit(taslak, Admin).Allowed);
        Assert.False(KnowledgePublishing.CanEdit(taslak, BaskaAgent).Allowed);
    }
}
