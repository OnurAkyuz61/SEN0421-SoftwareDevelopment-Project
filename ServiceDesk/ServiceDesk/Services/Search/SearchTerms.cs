using System.Globalization;
using System.Text.RegularExpressions;

namespace ServiceDesk.Services.Search;

// Arama metninin çözümlenmesi: kelimelere bölme, durak kelimeler, talep
// numarası ayrıştırma ve skor ağırlıkları.
//
// Bu değerler aramanın DAVRANIŞIDIR: "başlıkta geçmek gövdede geçmekten kaç
// kat değerli" kararı tek yerde durmalı, yoksa her ekran kendi sıralamasını
// uydurur (bilgi bankası arama, öneri ucu, chatbot ve global arama ayrı ayrı
// yazıldığında olan buydu).
//
// Saf sınıf: veritabanına ve HTTP'ye bağımlı değil, birim testi yazılabiliyor.
public static class SearchTerms
{
    // ───── Skor ağırlıkları ──────────────────────────────────────────────
    //
    // Başlıkta geçen kelime gövdede geçenden belirgin biçimde değerli:
    // "VPN" başlıklı makale, gövdesinde bir kez VPN yazan makaleden önce
    // gelmeli. Kategori adı ikisinin arasında: konuyu doğru söyler ama
    // makalenin kendisi hakkında az şey anlatır.
    public const int TitleWeight = 10;
    public const int BodyWeight = 2;
    public const int CategoryWeight = 3;

    // Başlık aranan kelimeyle BAŞLIYORSA ek puan: "yazıcı sıkışması" araması
    // "Yazıcı kağıt sıkışması giderme" makalesini öne çeker
    public const int TitlePrefixBonus = 5;

    // Çok kelimeli aramada kelimelerin TAMAMI bitişik geçiyorsa (öbek
    // eşleşmesi) ayrı puan: "kağıt sıkışması" arayan kişi, iki kelimeyi ayrı
    // ayrı barındıran makaleden önce tam ifadeyi barındıranı görmeli.
    public const int TitlePhraseWeight = 15;
    public const int BodyPhraseWeight = 4;

    // Talep alanları
    public const int TicketTitleWeight = 10;
    public const int TicketNumberWeight = 8;
    public const int TicketRequesterWeight = 2;

    // ───── Sınırlar ──────────────────────────────────────────────────────

    // Arama metninin tamamı okunmaz: çok uzun metin hem SQL parametresini
    // şişirir hem de anlamlı sonuç üretmez
    public const int MaxQueryLength = 200;

    // En fazla kaç kelime skora girer. Her kelime SQL'de ek CASE WHEN
    // demek; altı kelime hem yeterli hem de sorguyu makul tutuyor.
    public const int MaxWords = 6;

    // Tek harfli parça aranmaz: "a" neredeyse her makalede geçer
    public const int MinWordLength = 2;

    // Türkçe durak kelimeleri. Aramada hiçbir ayırt ediciliği yok; skora
    // girerse "nasıl" gibi bir kelime bütün makaleleri eşit puanlar.
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ve", "ile", "bir", "bu", "şu", "için", "ama", "gibi", "çok", "daha", "nasıl", "neden",
        "nedir", "niye", "var", "yok", "olan", "olarak", "ben", "benim", "bana", "beni", "sen",
        "biz", "değil", "ediyor", "olmuyor", "yapabilirim", "yardım", "lütfen", "merhaba"
    };

    public static bool IsStopWord(string word) => StopWords.Contains(word);

    // ───── Metnin düzenlenmesi ───────────────────────────────────────────

    // Arama TÜRKÇE küçültülür. "KAĞIT" sözcüğünün küçüğü "kağıt"tır,
    // "kağit" değil: kültürden bağımsız (invariant) küçültme I harfini
    // noktalı i'ye çevirir ve arama veritabanındaki "kağıt" ile hiç
    // eşleşmez. Aynı şekilde Türkçe klavyede yazılan "İNTERNET" doğru
    // biçimde "internet" olur. (Projede aynı tercih CurrentUser.Initials
    // içinde de var.)
    private static readonly CultureInfo Turkish = new("tr-TR");

    // Aranan metnin tek biçime indirgenmiş hali: baştaki/sondaki boşluklar
    // atılır, aradaki boşluklar teke iner, küçük harfe çevrilir.
    //
    // Küçük harfe çevirmenin sebebi SQL değil TEKRARLANABİLİRLİK: SQL
    // Server'ın harf duyarsız sıralama düzeni (collation) zaten büyük/küçük
    // harfe bakmaz, ama aynı skor kuralı birim testinde LINQ-to-Objects ile
    // de çalışıyor ve orada karşılaştırma harf duyarlıdır. İki tarafın aynı
    // sonucu vermesi için metin iki uçta da küçültülür.
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var s = Regex.Replace(text.Trim(), @"\s+", " ");
        if (s.Length > MaxQueryLength) s = s[..MaxQueryLength];
        return s.ToLower(Turkish);
    }

    // Aranan metnin anlamlı kelimeleri. Harf ve rakam dışındaki her şey
    // ayırıcı sayılır: "INC-2026-00042" → "inc", "2026", "00042".
    public static List<string> Words(string? text, int minLength = MinWordLength)
    {
        var normalized = Normalize(text);
        if (normalized.Length == 0) return new List<string>();

        return Regex.Split(normalized, @"[^\p{L}\p{N}]+")
            .Where(w => w.Length >= minLength && !IsStopWord(w))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxWords)
            .ToList();
    }

    // ───── Talep numarası ────────────────────────────────────────────────

    // Numara biçimleri (Ui.BuildTicketNumber): INC-2026-00042 / CHG-2026-00007.
    // Numaranın ayırt edici parçası son bölümdeki Id'dir; tür ve yıl
    // okunabilirlik içindir. Kullanıcı kısayol olarak "#42" ya da "42" de
    // yazabilir.
    // CultureInvariant ŞART: Türkçe kültürde "i" ile "I" birbirinin büyük /
    // küçük hali sayılmaz, bu yüzden kültüre duyarlı IgnoreCase ile
    // "inc-2026-00042" yazımı INC ile eşleşmez ve numara tanınmazdı.
    private static readonly Regex FullNumber =
        new(@"^(INC|CHG)-(\d{4})-(\d{1,9})$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ShortNumber =
        new(@"^#?(\d{1,9})$", RegexOptions.Compiled);

    // Aranan metin bir talep numarasına benziyor mu?
    //   "INC-2026-00042" → (42, "INC-2026-00042")
    //   "#42" / "42"     → (42, null)
    //   "yazıcı"         → null
    public sealed record TicketReference(int Id, string? Number);

    public static TicketReference? ParseTicketReference(string? text)
    {
        var s = (text ?? "").Trim();
        if (s.Length == 0) return null;

        var full = FullNumber.Match(s);
        if (full.Success)
        {
            // Id 0 olamaz: "INC-2026-00000" numara değildir.
            //
            // Büyütme burada INVARIANT olmalı (Normalize'ın tersine):
            // karşılaştırılacak değer Ui.BuildTicketNumber'ın ürettiği
            // "INC-..." metni ve Türkçe büyütme "i" harfini "İ" yapıp
            // "İNC" üretirdi — hiçbir taleple eşleşmeyen bir numara.
            return int.TryParse(full.Groups[3].Value, out var id) && id > 0
                ? new TicketReference(id, s.ToUpperInvariant())
                : null;
        }

        var kisa = ShortNumber.Match(s);
        if (kisa.Success)
            return int.TryParse(kisa.Groups[1].Value, out var id) && id > 0
                ? new TicketReference(id, null)
                : null;

        return null;
    }
}
