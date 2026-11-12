using System.Text;

namespace ServiceDesk.Services;

// appsettings.json → "Quality" bölümü.
//
// Eşikler daha önce NonconformityService içinde const olarak duruyordu:
// kurumun "hangi puan uygunsuzluk sayılır" kararını değiştirmesi için
// projenin yeniden derlenmesi gerekiyordu. ISO 9001 md. 10.2 eşiğin
// tanımlı olmasını ister, sabit olmasını değil.
public class QualitySettings
{
    public const string Section = "Quality";

    // Anket ORTALAMASI bu puanın altındaysa uygunsuzluk açılır
    public double LowSatisfactionThreshold { get; set; } = 2.5;

    // Tek bir sorunun bu puanı (ve altı) tek başına uygunsuzluk açar.
    // Ortalama eşiği tek bir kritik cevabı gizleyebilir: üç soruya 5, 5, 1
    // veren kullanıcının ortalaması 3,7'dir ve eşiğin üstünde kalır — ama
    // "sorunum çözülmedi" sorusuna verilen 1 puan kalite kaydı doğurmalıdır.
    // 0 = kapalı (yalnızca ortalamaya bakılır).
    public int CriticalQuestionScore { get; set; } = 1;

    // Düzeltici faaliyet için varsayılan termin (gün)
    public int CorrectiveActionDueDays { get; set; } = 7;
}

// Düşük memnuniyet uygunsuzluğunun kuralları ve açıklama metni.
//
// Saf sınıf: birim testi yazılabiliyor (bkz. ServiceDesk.Tests).
public static class NonconformityPolicy
{
    // Ankette tek bir sorunun metni ve puanı
    public sealed record AnswerScore(string Question, int Score);

    // Değerlendirmenin sonucu: ortalama eşiğin altında mı, hangi sorular
    // kritik düzeyde düşük puan almış.
    public sealed record LowSatisfactionFinding(
        bool AverageBelowThreshold,
        IReadOnlyList<AnswerScore> CriticalAnswers)
    {
        // Kayıt açılacak mı: iki gerekçeden biri yeterli
        public bool Triggered => AverageBelowThreshold || CriticalAnswers.Count > 0;
    }

    public static LowSatisfactionFinding Evaluate(double average, IEnumerable<AnswerScore> answers,
                                                  double threshold, int criticalScore)
    {
        var critical = criticalScore <= 0
            ? new List<AnswerScore>()
            : answers.Where(a => a.Score <= criticalScore)
                     .OrderBy(a => a.Score)
                     .ToList();

        return new LowSatisfactionFinding(average < threshold, critical);
    }

    // Uygunsuzluk açıklaması. Nonconformity.Description 500 karakterle
    // sınırlı; metin sırayla kurulur ve sonu kırpılır, böylece en önemli
    // bilgi (gerekçe ve soru kırılımı) her zaman içeride kalır:
    //   1) gerekçe (ortalama / kritik cevap)
    //   2) soru bazlı kırılım
    //   3) kullanıcı yorumu
    public static string Describe(string ticketNo, double average, LowSatisfactionFinding finding,
                                  IReadOnlyList<AnswerScore> answers, string? comment,
                                  double threshold, int maxLength = 500)
    {
        var sb = new StringBuilder();
        sb.Append($"{ticketNo} talebinde memnuniyet ortalaması {average:0.0}/5");

        if (finding.AverageBelowThreshold)
            sb.Append($" ({threshold:0.0} eşiğinin altında)");

        if (finding.CriticalAnswers.Count > 0)
        {
            var kritik = string.Join(", ", finding.CriticalAnswers.Select(a => $"\"{a.Question}\" {a.Score}/5"));
            sb.Append(finding.AverageBelowThreshold ? "; " : "; ortalama eşiğin üstünde olsa da ");
            sb.Append($"kritik düşük puan: {kritik}");
        }

        sb.Append('.');

        if (answers.Count > 0)
            sb.Append(" Soru bazında: ")
              .Append(string.Join(" · ", answers.Select(a => $"{a.Question} {a.Score}/5")))
              .Append('.');

        if (!string.IsNullOrWhiteSpace(comment))
            sb.Append($" Kullanıcı yorumu: \"{comment.Trim()}\"");

        return Cut(sb.ToString(), maxLength);
    }

    // Bildirim metni: sorumluya ne olduğu bir satırda söylenir
    public static string NotificationMessage(double average, LowSatisfactionFinding finding) =>
        finding.CriticalAnswers.Count > 0
            ? $"Memnuniyet ortalaması {average:0.0}/5; \"{finding.CriticalAnswers[0].Question}\" sorusu "
              + $"{finding.CriticalAnswers[0].Score}/5 puan aldı. Kök neden ve düzeltici faaliyet bekleniyor."
            : $"Memnuniyet ortalaması {average:0.0}/5. Kök neden ve düzeltici faaliyet bekleniyor.";

    internal static string Cut(string s, int max) => s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";
}
