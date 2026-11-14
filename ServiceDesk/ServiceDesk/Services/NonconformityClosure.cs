using System.Text;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Uygunsuzluğun KAPANIŞ kuralları (ISO 9001 md. 10.2).
//
// Standart düzeltici faaliyeti bir bütün olarak ister: kök nedenin
// belirlenmesi (10.2.1 b), alınan önlem (10.2.1 c), önlemin ETKİNLİĞİNİN
// gözden geçirilmesi (10.2.1 d) ve bunların KAYDI (10.2.2). Eskiden
// controller yalnızca ilk ikisine bakıyordu; etkinlik doğrulaması iz bırakmayan
// bir onay kutusuydu ve kapanışın hiçbir kanıtı istenmiyordu.
//
// Kural controller'ın içine gömülü kalsa kayıt başka bir yoldan (API, içe
// aktarma, arka plan işi) kapatıldığında atlanabilirdi; tek giriş noktası
// burası.
//
// Saf sınıf: veritabanına ve HTTP'ye bağımlı değil, bu yüzden birim testi
// yazılabiliyor (bkz. ServiceDesk.Tests).
public static class NonconformityClosure
{
    // Doğrulamanın kayda işlenmesinin sonucu. Hata varsa doğrulama YAZILMAZ;
    // çağıran kaydı kapatmaz ama kullanıcının girdiği diğer alanları korur.
    public sealed record VerificationResult(bool Verified, string? Error);

    // ───── Etkinlik doğrulaması ──────────────────────────────────────────

    // Doğrulamayı kayda işler. İki kural var:
    //
    //   • Doğrulayan kişi SORUMLUDAN farklı olmalı. Düzeltici faaliyeti
    //     yürüten kişinin "etkili oldu" beyanı gözden geçirme değildir;
    //     md. 10.2 ikinci bir gözle doğrulamayı ister (bilgi bankasında
    //     yazarın kendi makalesini onaylayamaması ile aynı mantık).
    //   • Doğrulamanın NOTU zorunlu: neye bakılarak doğrulandığı yazılmazsa
    //     kayıt yine iz bırakmayan bir bayraktan ibaret olur.
    //
    // Doğrulama bir kez yazılır: sonraki kaydetmelerde "kim, ne zaman"
    // değişmez (formu başka bir personel kaydetse de doğrulayan o olmaz),
    // yalnızca not güncellenebilir.
    //
    // İşaret kaldırıldığında doğrulama GERİ ALINIR ve izi de düşer: aksi
    // halde "doğrulanmadı" ama "şu kişi doğruladı" diyen çelişkili bir kayıt
    // kalırdı. Kayıt kapanmışsa geri alınamaz; bunu çağıran engeller
    // (kapanmış kayıt zaten düzenlenmez).
    public static VerificationResult ApplyVerification(
        Nonconformity n, bool requested, int actorId, int? responsibleId,
        string? note, DateTime nowUtc)
    {
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (!requested)
        {
            n.EffectivenessVerified = null;
            n.VerifiedAt = null;
            n.VerifiedById = null;
            n.VerificationNote = null;
            return new VerificationResult(false, null);
        }

        var zatenDogrulandi = n.EffectivenessVerified == true && n.VerifiedById != null;

        // Yeni doğrulamada doğrulayan oturumdaki kişidir; sorumluysa reddedilir
        if (!zatenDogrulandi && responsibleId != null && responsibleId == actorId)
            return new VerificationResult(false,
                "Etkinlik doğrulamasını düzeltici faaliyetin sorumlusu yapamaz; " +
                "doğrulamayı başka bir personel kaydetmeli.");

        if (note == null && n.VerificationNote == null)
            return new VerificationResult(false,
                "Etkinliğin nasıl doğrulandığını (doğrulama notu) yazmanız gerekiyor.");

        n.EffectivenessVerified = true;
        if (!zatenDogrulandi)
        {
            n.VerifiedById = actorId;
            n.VerifiedAt = nowUtc;
        }
        if (note != null) n.VerificationNote = note;

        return new VerificationResult(true, null);
    }

    // Kayıtlı doğrulamayla çelişen sorumlu ataması. Doğrulamayı yapan kişi
    // sonradan sorumlu seçilirse "ayrı göz" kuralı sessizce bozulurdu.
    public static string? CheckResponsible(Nonconformity n, int? responsibleId) =>
        responsibleId != null && n.EffectivenessVerified == true && n.VerifiedById == responsibleId
            ? "Etkinliği doğrulayan kişi aynı uygunsuzluğun sorumlusu olarak seçilemez; " +
              "doğrulamanın ayrı bir göz tarafından yapılması gerekiyor."
            : null;

    // ───── Kapanış ───────────────────────────────────────────────────────

    // Kapanış için eksik olan zorunlu alanlar. Boş liste = kapatılabilir.
    //
    // evidenceCount: kaldırılmamış ve taramadan TEMİZ geçmiş kanıt dosyası
    // sayısı. Şüpheli / taranamamış dosya kanıt sayılmaz: indirilemeyen bir
    // dosya denetimde gösterilemez.
    public static List<string> MissingForClose(Nonconformity n, int evidenceCount)
    {
        var eksik = new List<string>();

        if (string.IsNullOrWhiteSpace(n.RootCause)) eksik.Add("kök neden");
        if (string.IsNullOrWhiteSpace(n.CorrectiveAction)) eksik.Add("düzeltici faaliyet");
        if (n.EffectivenessVerified != true || n.VerifiedById == null) eksik.Add("etkinlik doğrulaması");
        if (evidenceCount < 1) eksik.Add("kanıt dosyası");

        return eksik;
    }

    // "kök neden, etkinlik doğrulaması ve kanıt dosyası" — eksik alan sayısı
    // ikiden fazla olabildiği için düz " ve " birleştirmesi okunmaz oluyordu.
    public static string Join(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " ve " + items[^1]
    };

    // Kanıt dosyası kaldırılabilir mi: kapanmış uygunsuzluğun kanıtı
    // dondurulur (md. 7.5 — kapanış kaydının içeriği sonradan değişmez).
    public static string? CheckEvidenceRemoval(Nonconformity n, NonconformityAttachment file)
    {
        if (file.IsDeleted) return "Bu dosya zaten kaldırılmış.";

        return n.Status == NonconformityStatus.Closed
            ? "Kapanmış uygunsuzluğun kanıt dosyası kaldırılamaz; kayıt denetim izidir."
            : null;
    }

    // ───── Bilgi bankası makalesi ────────────────────────────────────────

    // Uygunsuzluk makaleye dönüştürülebilir mi: kök neden ve düzeltici
    // faaliyet olmadan makalenin gövdesi boş kalırdı.
    public static string? CheckArticleCreation(Nonconformity n)
    {
        if (n.ArticleId != null)
            return "Bu uygunsuzluktan zaten bir makale oluşturulmuş.";

        return string.IsNullOrWhiteSpace(n.RootCause) || string.IsNullOrWhiteSpace(n.CorrectiveAction)
            ? "Makale oluşturmak için kök neden ve düzeltici faaliyet yazılmış olmalı."
            : null;
    }

    // Makale başlığı: taslak için başlangıç metni, yazar düzenler.
    // KnowledgeArticle.Title 200 karakterle sınırlı.
    public static string ArticleTitle(Nonconformity n) =>
        NonconformityPolicy.Cut("Kalıcı çözüm: " + FirstSentence(n.Description), 200);

    // Makale gövdesi: kök neden ve düzeltici faaliyet ön-doldurulur, uygunsuzluk
    // kaydına atıf bırakılır. Metin düz yazıdır (makale gövdesi white-space:
    // pre-line ile gösteriliyor), HTML üretilmez.
    public static string ArticleBody(Nonconformity n)
    {
        var sb = new StringBuilder();

        sb.Append("Bu makale #").Append(n.Id)
          .Append(" numaralı uygunsuzluk kaydından oluşturuldu (tespit: ")
          .Append(AppTime.ToLocal(n.DetectedAt).ToString("dd.MM.yyyy")).AppendLine(").");
        sb.AppendLine();

        sb.AppendLine("Belirti / uygunsuzluk").AppendLine(n.Description.Trim()).AppendLine();
        sb.AppendLine("Kök neden").AppendLine((n.RootCause ?? "").Trim()).AppendLine();
        sb.AppendLine("Kalıcı çözüm / düzeltici faaliyet").AppendLine((n.CorrectiveAction ?? "").Trim());

        if (!string.IsNullOrWhiteSpace(n.VerificationNote))
            sb.AppendLine().AppendLine("Etkinlik doğrulaması").AppendLine(n.VerificationNote.Trim());

        return sb.ToString().TrimEnd();
    }

    // Açıklamanın ilk cümlesi: başlık tüm açıklamayı taşımasın
    private static string FirstSentence(string text)
    {
        var s = text.Trim();
        var i = s.IndexOfAny(new[] { '.', ':', '\n' });
        return i > 0 ? s[..i].Trim() : s;
    }
}
