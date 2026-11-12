using ServiceDesk.Services;
using static ServiceDesk.Services.NonconformityPolicy;

namespace ServiceDesk.Tests;

// Düşük memnuniyet uygunsuzluğunun iki gerekçesi: ortalamanın eşik altı
// olması ve TEK bir sorunun kritik düşük puanı.
public class NonconformityPolicyTests
{
    private const double Threshold = 2.5;
    private const int CriticalScore = 1;

    private static readonly List<AnswerScore> Yuksek = new()
    {
        new("Çözüm hızı", 5),
        new("Kalıcı çözüm", 5),
        new("İlgi", 4)
    };

    [Fact]
    public void Yuksek_puanli_anket_uygunsuzluk_acmaz()
    {
        var f = Evaluate(average: 4.7, Yuksek, Threshold, CriticalScore);

        Assert.False(f.Triggered);
        Assert.False(f.AverageBelowThreshold);
        Assert.Empty(f.CriticalAnswers);
    }

    [Fact]
    public void Ortalama_esik_altindaysa_uygunsuzluk_acilir()
    {
        var answers = new List<AnswerScore> { new("Çözüm hızı", 2), new("İlgi", 2), new("Kalıcı çözüm", 3) };

        var f = Evaluate(average: 2.3, answers, Threshold, CriticalScore);

        Assert.True(f.Triggered);
        Assert.True(f.AverageBelowThreshold);
    }

    [Fact]
    public void Tek_sorunun_kritik_puani_ortalama_yeterken_de_uygunsuzluk_acar()
    {
        // 5, 5, 1 → ortalama 3,7: eşiğin ÜSTÜNDE ama "kalıcı çözüm" 1 puan.
        // Eski kural bu anketi hiç görmüyordu.
        var answers = new List<AnswerScore> { new("Çözüm hızı", 5), new("İlgi", 5), new("Kalıcı çözüm", 1) };

        var f = Evaluate(average: 3.67, answers, Threshold, CriticalScore);

        Assert.True(f.Triggered);
        Assert.False(f.AverageBelowThreshold);
        Assert.Single(f.CriticalAnswers);
        Assert.Equal("Kalıcı çözüm", f.CriticalAnswers[0].Question);
    }

    [Fact]
    public void Kritik_puan_kurali_kapatilabilir()
    {
        var answers = new List<AnswerScore> { new("Çözüm hızı", 5), new("Kalıcı çözüm", 1) };

        var f = Evaluate(average: 3.0, answers, Threshold, criticalScore: 0);

        Assert.False(f.Triggered);
        Assert.Empty(f.CriticalAnswers);
    }

    [Fact]
    public void Kritik_cevaplar_en_dusuk_puandan_siralanir()
    {
        var answers = new List<AnswerScore> { new("A", 2), new("B", 1), new("C", 2) };

        var f = Evaluate(average: 1.7, answers, Threshold, criticalScore: 2);

        Assert.Equal(new[] { 1, 2, 2 }, f.CriticalAnswers.Select(a => a.Score));
        Assert.Equal("B", f.CriticalAnswers[0].Question);
    }

    // ───── Açıklama metni ────────────────────────────────────────────────

    [Fact]
    public void Aciklamada_soru_kirilimi_ve_yorum_yer_alir()
    {
        var answers = new List<AnswerScore> { new("Çözüm hızı", 1), new("İlgi", 2) };
        var f = Evaluate(average: 1.5, answers, Threshold, CriticalScore);

        var text = Describe("INC-2026-00042", 1.5, f, answers, "Üç kez aradım, dönen olmadı.", Threshold);

        Assert.Contains("INC-2026-00042", text);
        Assert.Contains("1,5/5", text);
        Assert.Contains("Soru bazında:", text);
        Assert.Contains("Çözüm hızı 1/5", text);
        Assert.Contains("İlgi 2/5", text);
        Assert.Contains("Üç kez aradım", text);
    }

    [Fact]
    public void Ortalama_yeterken_acilan_kayitta_gerekce_kritik_cevap_oldugu_yazilir()
    {
        var answers = new List<AnswerScore> { new("Çözüm hızı", 5), new("Kalıcı çözüm", 1) };
        var f = Evaluate(average: 3.0, answers, Threshold, CriticalScore);

        var text = Describe("INC-2026-00007", 3.0, f, answers, comment: null, Threshold);

        Assert.Contains("kritik düşük puan", text);
        Assert.Contains("ortalama eşiğin üstünde olsa da", text);
        Assert.DoesNotContain("eşiğinin altında", text);
    }

    [Fact]
    public void Aciklama_veritabani_sinirini_asmaz()
    {
        // Nonconformity.Description 500 karakter
        var answers = Enumerable.Range(1, 20)
            .Select(i => new AnswerScore(new string('S', 60) + i, 1))
            .ToList();
        var f = Evaluate(average: 1.0, answers, Threshold, CriticalScore);

        var text = Describe("INC-2026-00042", 1.0, f, answers, new string('Y', 400), Threshold);

        Assert.True(text.Length <= 500, $"uzunluk {text.Length}");
        Assert.EndsWith("…", text);
        // Kırpılsa bile gerekçe baştadır
        Assert.StartsWith("INC-2026-00042", text);
    }

    [Fact]
    public void Yorum_yoksa_aciklamada_yorum_bolumu_olmaz()
    {
        var answers = new List<AnswerScore> { new("Çözüm hızı", 2) };
        var f = Evaluate(average: 2.0, answers, Threshold, CriticalScore);

        var text = Describe("INC-2026-00001", 2.0, f, answers, "   ", Threshold);

        Assert.DoesNotContain("Kullanıcı yorumu", text);
    }

    [Fact]
    public void Bildirim_metni_kritik_soruyu_oncelikle_soyler()
    {
        var answers = new List<AnswerScore> { new("Çözüm hızı", 5), new("Kalıcı çözüm", 1) };
        var f = Evaluate(average: 3.0, answers, Threshold, CriticalScore);

        var message = NotificationMessage(3.0, f);

        Assert.Contains("Kalıcı çözüm", message);
        Assert.Contains("1/5", message);
    }
}
