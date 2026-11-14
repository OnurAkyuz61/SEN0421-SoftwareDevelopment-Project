using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Uygunsuzluğun kapanış kuralları (ISO 9001 md. 10.2): kök neden, düzeltici
// faaliyet, etkinlik doğrulaması ve kanıt dosyası — dördü birlikte.
public class NonconformityClosureTests
{
    private const int Sorumlu = 7;
    private const int Dogrulayan = 9;

    private static readonly DateTime Now = new(2026, 11, 14, 8, 0, 0, DateTimeKind.Utc);

    private static Nonconformity Kayit(bool tamam = true) => new()
    {
        Id = 42,
        Description = "INC-2026-00042 talebinde hizmet seviyesi aşıldı: çözüm zamanında tamamlanmadı.",
        Status = NonconformityStatus.InProgress,
        ResponsibleId = Sorumlu,
        DetectedAt = Now.AddDays(-10),
        RootCause = tamam ? "Yedek parça stokta yoktu." : null,
        CorrectiveAction = tamam ? "Kritik parçalar için asgari stok tanımlandı." : null
    };

    private static Nonconformity Dogrulanmis()
    {
        var n = Kayit();
        NonconformityClosure.ApplyVerification(
            n, requested: true, actorId: Dogrulayan, responsibleId: Sorumlu,
            note: "30 gün boyunca aynı ihlal tekrarlamadı.", nowUtc: Now);
        return n;
    }

    // ───── Etkinlik doğrulaması ──────────────────────────────────────────

    [Fact]
    public void Dogrulama_kim_ne_zaman_ve_neye_dayanarak_bilgisini_birakir()
    {
        var n = Dogrulanmis();

        Assert.True(n.EffectivenessVerified);
        Assert.Equal(Dogrulayan, n.VerifiedById);
        Assert.Equal(Now, n.VerifiedAt);
        Assert.Equal("30 gün boyunca aynı ihlal tekrarlamadı.", n.VerificationNote);
    }

    [Fact]
    public void Sorumlu_kendi_faaliyetinin_etkinligini_dogrulayamaz()
    {
        var n = Kayit();

        var sonuc = NonconformityClosure.ApplyVerification(
            n, requested: true, actorId: Sorumlu, responsibleId: Sorumlu,
            note: "Kendim kontrol ettim.", nowUtc: Now);

        Assert.False(sonuc.Verified);
        Assert.NotNull(sonuc.Error);

        // Doğrulama hiç yazılmaz: yarım kayıt kalmaz
        Assert.Null(n.VerifiedById);
        Assert.Null(n.VerifiedAt);
        Assert.Null(n.VerificationNote);
    }

    [Fact]
    public void Dogrulama_notu_zorunlu()
    {
        var n = Kayit();

        var sonuc = NonconformityClosure.ApplyVerification(
            n, requested: true, actorId: Dogrulayan, responsibleId: Sorumlu,
            note: "   ", nowUtc: Now);

        Assert.False(sonuc.Verified);
        Assert.NotNull(sonuc.Error);
        Assert.Null(n.VerifiedById);
    }

    [Fact]
    public void Dogrulayan_sonraki_kaydetmelerde_degismez()
    {
        var n = Dogrulanmis();

        // Formu başka bir personel kaydediyor
        var sonuc = NonconformityClosure.ApplyVerification(
            n, requested: true, actorId: 11, responsibleId: Sorumlu,
            note: "Stok sayımı kaydı eklendi.", nowUtc: Now.AddDays(1));

        Assert.True(sonuc.Verified);
        Assert.Equal(Dogrulayan, n.VerifiedById);          // doğrulayan ilk kişi kalır
        Assert.Equal(Now, n.VerifiedAt);                   // doğrulama zamanı da
        Assert.Equal("Stok sayımı kaydı eklendi.", n.VerificationNote);  // not güncellenir
    }

    [Fact]
    public void Isaret_kaldirilirsa_dogrulama_izi_de_duser()
    {
        var n = Dogrulanmis();

        var sonuc = NonconformityClosure.ApplyVerification(
            n, requested: false, actorId: Dogrulayan, responsibleId: Sorumlu,
            note: null, nowUtc: Now);

        Assert.False(sonuc.Verified);
        Assert.Null(sonuc.Error);
        Assert.Null(n.EffectivenessVerified);
        Assert.Null(n.VerifiedById);
        Assert.Null(n.VerifiedAt);
        Assert.Null(n.VerificationNote);
    }

    [Fact]
    public void Dogrulayan_kisi_sonradan_sorumlu_secilemez()
    {
        var n = Dogrulanmis();

        Assert.NotNull(NonconformityClosure.CheckResponsible(n, Dogrulayan));
        Assert.Null(NonconformityClosure.CheckResponsible(n, Sorumlu));
        Assert.Null(NonconformityClosure.CheckResponsible(n, null));
    }

    // ───── Kapanış şartları ──────────────────────────────────────────────

    [Fact]
    public void Dort_sart_tamamsa_kapatilabilir()
    {
        var eksik = NonconformityClosure.MissingForClose(Dogrulanmis(), evidenceCount: 1);

        Assert.Empty(eksik);
    }

    [Fact]
    public void Kok_neden_ve_duzeltici_faaliyet_olmadan_kapanmaz()
    {
        var n = Dogrulanmis();
        n.RootCause = null;
        n.CorrectiveAction = "   ";

        var eksik = NonconformityClosure.MissingForClose(n, evidenceCount: 3);

        Assert.Contains("kök neden", eksik);
        Assert.Contains("düzeltici faaliyet", eksik);
    }

    [Fact]
    public void Dogrulama_olmadan_kapanmaz()
    {
        var eksik = NonconformityClosure.MissingForClose(Kayit(), evidenceCount: 1);

        Assert.Contains("etkinlik doğrulaması", eksik);
    }

    // İşaret elle true yazılmış ama doğrulayan kaydı yok (geçişten önceki
    // kayıtlar): doğrulama sayılmaz
    [Fact]
    public void Izi_olmayan_dogrulama_isareti_yetmez()
    {
        var n = Kayit();
        n.EffectivenessVerified = true;

        Assert.Contains("etkinlik doğrulaması", NonconformityClosure.MissingForClose(n, evidenceCount: 1));
    }

    [Fact]
    public void Kanit_dosyasi_olmadan_kapanmaz()
    {
        var eksik = NonconformityClosure.MissingForClose(Dogrulanmis(), evidenceCount: 0);

        Assert.Equal(new[] { "kanıt dosyası" }, eksik);
    }

    [Fact]
    public void Eksik_alanlar_okunur_bir_cumleye_cevrilir()
    {
        Assert.Equal("kök neden", NonconformityClosure.Join(new[] { "kök neden" }));
        Assert.Equal("kök neden ve kanıt dosyası",
            NonconformityClosure.Join(new[] { "kök neden", "kanıt dosyası" }));
        Assert.Equal("kök neden, düzeltici faaliyet ve kanıt dosyası",
            NonconformityClosure.Join(new[] { "kök neden", "düzeltici faaliyet", "kanıt dosyası" }));
    }

    // ───── Kanıt dosyasının kaldırılması ─────────────────────────────────

    [Fact]
    public void Kapanmis_kaydin_kaniti_kaldirilamaz()
    {
        var n = Dogrulanmis();
        n.Status = NonconformityStatus.Closed;
        var dosya = new NonconformityAttachment { Id = 1, FileName = "stok-sayimi.pdf" };

        Assert.NotNull(NonconformityClosure.CheckEvidenceRemoval(n, dosya));

        n.Status = NonconformityStatus.InProgress;
        Assert.Null(NonconformityClosure.CheckEvidenceRemoval(n, dosya));
    }

    // ───── Bilgi bankası makalesi ────────────────────────────────────────

    [Fact]
    public void Makale_kok_neden_ve_duzeltici_faaliyet_olmadan_olusturulamaz()
    {
        Assert.NotNull(NonconformityClosure.CheckArticleCreation(Kayit(tamam: false)));
        Assert.Null(NonconformityClosure.CheckArticleCreation(Kayit()));
    }

    [Fact]
    public void Ayni_uygunsuzluktan_ikinci_makale_olusturulmaz()
    {
        var n = Kayit();
        n.ArticleId = 5;

        Assert.NotNull(NonconformityClosure.CheckArticleCreation(n));
    }

    [Fact]
    public void Makale_govdesi_kok_neden_ve_duzeltici_faaliyetle_on_doldurulur()
    {
        var n = Dogrulanmis();

        var govde = NonconformityClosure.ArticleBody(n);

        Assert.Contains("Yedek parça stokta yoktu.", govde);
        Assert.Contains("Kritik parçalar için asgari stok tanımlandı.", govde);
        Assert.Contains("Kök neden", govde);
        Assert.Contains("#42", govde);                                  // kayda atıf
        Assert.Contains("30 gün boyunca aynı ihlal tekrarlamadı.", govde);  // doğrulama notu
    }

    [Fact]
    public void Makale_basligi_200_karakteri_gecmez()
    {
        var n = Kayit();
        n.Description = new string('a', 600);

        var baslik = NonconformityClosure.ArticleTitle(n);

        Assert.True(baslik.Length <= 200, $"başlık {baslik.Length} karakter");
        Assert.StartsWith("Kalıcı çözüm:", baslik);
    }
}
