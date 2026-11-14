using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Anket şablonu yönetiminin kuralları.
//
// En kritiği tek aktif şablon: PortalController.Feedback aktif şablonu
// FirstOrDefaultAsync(t => t.IsActive) ile çeker, iki şablon birlikte aktifse
// hangisinin doldurulacağı veritabanının sırasına kalır.
public class SurveyTemplateRulesTests
{
    // ───── Şablon aktifleştirme ──────────────────────────────────────────

    [Fact]
    public void Baska_sablon_aktifken_ikinci_sablon_aktif_edilemez()
    {
        var hata = SurveyTemplateRules.ActivationError("Destek Memnuniyet Anketi", activeQuestionCount: 3);

        Assert.NotNull(hata);
        Assert.Contains("Destek Memnuniyet Anketi", hata);
    }

    [Fact]
    public void Aktif_sorusu_olmayan_sablon_aktif_edilemez()
    {
        Assert.NotNull(SurveyTemplateRules.ActivationError(null, activeQuestionCount: 0));
    }

    [Fact]
    public void Tek_basina_ve_sorusu_olan_sablon_aktif_edilir()
    {
        Assert.Null(SurveyTemplateRules.ActivationError(null, activeQuestionCount: 1));
    }

    // ───── Soru pasifleştirme ────────────────────────────────────────────

    [Fact]
    public void Aktif_sablonun_son_aktif_sorusu_pasiflestirilemez()
    {
        Assert.NotNull(SurveyTemplateRules.QuestionDeactivationError(templateActive: true, otherActiveQuestionCount: 0));
    }

    [Fact]
    public void Aktif_sablonda_baska_soru_varsa_pasiflestirilebilir()
    {
        Assert.Null(SurveyTemplateRules.QuestionDeactivationError(templateActive: true, otherActiveQuestionCount: 2));
    }

    [Fact]
    public void Pasif_sablonun_son_sorusu_pasiflestirilebilir()
    {
        // Şablon zaten yürürlükte değil: portalda anket görünmüyor
        Assert.Null(SurveyTemplateRules.QuestionDeactivationError(templateActive: false, otherActiveQuestionCount: 0));
    }

    // ───── Sıra numarası ─────────────────────────────────────────────────

    [Fact]
    public void Yeni_sorunun_sirasi_en_buyugun_bir_adim_ustu()
    {
        Assert.Equal(40, SurveyTemplateRules.NextSortOrder(new[] { 10, 30, 20 }));
    }

    [Fact]
    public void Ilk_sorunun_sirasi_bir_adim()
    {
        Assert.Equal(SurveyTemplateRules.SortStep, SurveyTemplateRules.NextSortOrder(Array.Empty<int>()));
    }

    // ───── Taşıma ────────────────────────────────────────────────────────

    [Fact]
    public void Yukari_tasima_onceki_soruyla_yer_degistirir()
    {
        var sira = SurveyTemplateRules.Move(new[] { 1, 2, 3 }, id: 3, delta: -1);
        Assert.Equal(new[] { 1, 3, 2 }, sira);
    }

    [Fact]
    public void Asagi_tasima_sonraki_soruyla_yer_degistirir()
    {
        var sira = SurveyTemplateRules.Move(new[] { 1, 2, 3 }, id: 1, delta: 1);
        Assert.Equal(new[] { 2, 1, 3 }, sira);
    }

    [Theory]
    [InlineData(1, -1)]   // ilk kayıt yukarı
    [InlineData(3, 1)]    // son kayıt aşağı
    public void Uctaki_soru_tasinmaz(int id, int delta)
    {
        Assert.Equal(new[] { 1, 2, 3 }, SurveyTemplateRules.Move(new[] { 1, 2, 3 }, id, delta));
    }

    [Fact]
    public void Listede_olmayan_soru_sirayi_bozmaz()
    {
        Assert.Equal(new[] { 1, 2, 3 }, SurveyTemplateRules.Move(new[] { 1, 2, 3 }, id: 99, delta: -1));
    }

    // Eşit SortOrder'da komşu takası hiçbir şeyi değiştirmezdi; taşıma
    // değer takası değil, liste üzerinden yeniden numaralandırma yapıyor
    [Fact]
    public void Esit_sira_numaralari_yeniden_numaralandirilir()
    {
        var sira = SurveyTemplateRules.Move(new[] { 7, 8, 9 }, id: 9, delta: -1);

        Assert.Equal(new[] { 7, 9, 8 }, sira);

        var yeniDegerler = sira.Select((_, i) => (i + 1) * SurveyTemplateRules.SortStep).ToArray();
        Assert.Equal(new[] { 10, 20, 30 }, yeniDegerler);
    }
}
