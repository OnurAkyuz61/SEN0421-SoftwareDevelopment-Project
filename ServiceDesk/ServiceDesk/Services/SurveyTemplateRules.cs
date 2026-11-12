namespace ServiceDesk.Services;

// Anket şablonu ve soru yönetiminin kuralları.
//
// En önemlisi TEK AKTİF ŞABLON kuralı: PortalController.Feedback aktif
// şablonu FirstOrDefaultAsync(t => t.IsActive) ile çeker, yani iki şablon
// birlikte aktifse hangisinin doldurulacağı veritabanının döndürme sırasına
// kalır — aynı dönemin yanıtları iki farklı şablona dağılır ve soru bazındaki
// ortalamalar karşılaştırılamaz hale gelir (ISO 9001 md. 9.1.2: memnuniyet
// ölçümü izlenebilir olmalı). Kural bu yüzden "ikinci şablon da aktif
// edilebilir ama tavsiye edilmez" değil, doğrudan engeldir.
//
// Saf sınıf: birim testi yazılabiliyor (bkz. ServiceDesk.Tests).
public static class SurveyTemplateRules
{
    // Sıra numaraları 10'un katları olarak yeniden yazılır: aradaki boşluk
    // elle araya soru sokmayı kolaylaştırır
    public const int SortStep = 10;

    public const int MaxNameLength = 100;    // SurveyTemplate.Name
    public const int MaxQuestionLength = 200; // SurveyQuestion.Text

    // Şablon aktif edilebilir mi? Hata varsa mesajı, yoksa null döner.
    //
    // İki koşul: başka aktif şablon olmamalı ve şablonun en az bir aktif
    // sorusu olmalı — sorusu olmayan aktif şablon portalda anket formunu hiç
    // göstermez, yönetici ise "anket açık" sanır.
    public static string? ActivationError(string? otherActiveTemplateName, int activeQuestionCount)
    {
        if (otherActiveTemplateName != null)
            return $"\"{otherActiveTemplateName}\" şablonu aktif. Aynı anda yalnızca bir anket şablonu aktif olabilir: "
                 + "yeni şablonu aktif etmek için önce onu pasifleştirin.";

        if (activeQuestionCount == 0)
            return "Şablonun aktif sorusu yok. Aktif etmeden önce en az bir soru ekleyin.";

        return null;
    }

    // Soru pasifleştirilebilir mi? Aktif şablonun son aktif sorusu
    // kapatılamaz: şablon "aktif ama sorusuz" kalır, portalda anket görünmez
    // ve kimse fark etmez. Anketi durdurmanın doğru yolu şablonu
    // pasifleştirmektir.
    public static string? QuestionDeactivationError(bool templateActive, int otherActiveQuestionCount) =>
        templateActive && otherActiveQuestionCount == 0
            ? "Bu, aktif şablonun son aktif sorusu. Anketi durdurmak için soruyu değil şablonu pasifleştirin."
            : null;

    // Yeni sorunun sıra numarası: en büyüğün bir adım üstü
    public static int NextSortOrder(IEnumerable<int> existingSortOrders)
    {
        var max = 0;
        foreach (var s in existingSortOrders)
            if (s > max) max = s;
        return max + SortStep;
    }

    // Taşıma: görüntü sırasındaki id listesinde kaydı delta kadar kaydırır
    // (-1 yukarı, +1 aşağı) ve yeni sırayı döner. Uçta ya da liste dışı bir
    // id ile sıra aynen döner.
    //
    // Sıra tek tek SortOrder'a dokunarak değil, liste üzerinden hesaplanır:
    // iki sorunun SortOrder'ı eşit kaldıysa (elle veri girilmiş, seed'den
    // gelmiş) basit bir değer takası hiçbir şeyi değiştirmezdi. Çağıran
    // dönen sırayı 10, 20, 30 … olarak yeniden numaralandırır.
    public static List<int> Move(IReadOnlyList<int> orderedIds, int id, int delta)
    {
        var list = orderedIds.ToList();
        var from = list.IndexOf(id);
        if (from < 0 || delta == 0) return list;

        var to = from + delta;
        if (to < 0 || to >= list.Count) return list;

        (list[from], list[to]) = (list[to], list[from]);
        return list;
    }
}
