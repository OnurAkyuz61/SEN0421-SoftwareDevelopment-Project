using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Search;

namespace ServiceDesk.Tests;

// Arama katmanı: metnin kelimelere bölünmesi, talep numarası biçimleri ve
// skor sıralaması.
//
// Skor bir LINQ ifadesi olarak kurulduğu için aynı kural bellekte de
// çalışıyor: aşağıdaki testler listeyi AsQueryable() ile sorgulayıp
// sıralamayı veritabanı olmadan doğruluyor.
public class SearchTermsTests
{
    [Fact]
    public void Bosluklar_sadelesir_ve_kucuk_harfe_duser()
    {
        Assert.Equal("yazıcı kağıt sıkışması", SearchTerms.Normalize("  Yazıcı   KAĞIT\tsıkışması \n"));
        Assert.Equal("", SearchTerms.Normalize(null));
        Assert.Equal("", SearchTerms.Normalize("   "));
    }

    [Fact]
    public void Cok_uzun_metin_kirpilir() =>
        Assert.Equal(SearchTerms.MaxQueryLength, SearchTerms.Normalize(new string('a', 500)).Length);

    [Fact]
    public void Kelimeler_ayrilir_durak_kelimeler_atilir()
    {
        var kelimeler = SearchTerms.Words("Yazıcıda kağıt sıkışması nasıl giderilir");

        Assert.Contains("yazıcıda", kelimeler);
        Assert.Contains("sıkışması", kelimeler);
        Assert.DoesNotContain("nasıl", kelimeler);   // durak kelime
    }

    [Fact]
    public void Tek_harfli_parca_ve_tekrar_atilir()
    {
        var kelimeler = SearchTerms.Words("a vpn vpn b bağlantı");

        Assert.Equal(new[] { "vpn", "bağlantı" }, kelimeler);
    }

    [Fact]
    public void Kelime_sayisi_sinirli() =>
        Assert.Equal(SearchTerms.MaxWords,
            SearchTerms.Words("bir iki üç dört beş altı yedi sekiz dokuz").Count);

    [Fact]
    public void Noktalama_ayirici_sayilir() =>
        Assert.Equal(new[] { "ağ", "internet" }, SearchTerms.Words("Ağ / İnternet"));

    // Küçültme TÜRKÇE: "İ" → "i", "I" → "ı". Bu yüzden "INC-2026-00042"
    // kelimelere bölündüğünde "ınc" çıkar — numara ayrıştırması zaten bu
    // yoldan geçmez (ParseTicketReference metnin kendisine bakar), ama
    // kuralın bilinçli olduğu burada kayıtlı kalsın.
    [Fact]
    public void Kucultme_turkce_kurallarina_uyar()
    {
        Assert.Equal(new[] { "ınc", "2026", "00042" }, SearchTerms.Words("INC-2026-00042"));
        Assert.Equal("kağıt", SearchTerms.Normalize("KAĞIT"));
        Assert.Equal("internet", SearchTerms.Normalize("İNTERNET"));
    }

    [Fact]
    public void Aramasiz_metinde_kelime_yok()
    {
        Assert.Empty(SearchTerms.Words(null));
        Assert.Empty(SearchTerms.Words("  "));
        Assert.Empty(SearchTerms.Words("ve ile bir"));   // hepsi durak kelime
    }

    // ───── Talep numarası ────────────────────────────────────────────────

    [Theory]
    [InlineData("INC-2026-00042", 42, "INC-2026-00042")]
    [InlineData("inc-2026-00042", 42, "INC-2026-00042")]   // küçük harf de kabul
    [InlineData("CHG-2026-00007", 7, "CHG-2026-00007")]
    [InlineData("  INC-2026-00042  ", 42, "INC-2026-00042")]
    public void Tam_numara_ayristirilir(string girdi, int beklenenId, string beklenenNumara)
    {
        var r = SearchTerms.ParseTicketReference(girdi);

        Assert.NotNull(r);
        Assert.Equal(beklenenId, r!.Id);
        Assert.Equal(beklenenNumara, r.Number);
    }

    [Theory]
    [InlineData("#42", 42)]
    [InlineData("42", 42)]
    [InlineData(" #7 ", 7)]
    public void Kisa_biçim_ayristirilir(string girdi, int beklenenId)
    {
        var r = SearchTerms.ParseTicketReference(girdi);

        Assert.NotNull(r);
        Assert.Equal(beklenenId, r!.Id);
        // Kısa biçimde numara kolonuyla eşleşme aranmaz, yalnızca Id
        Assert.Null(r.Number);
    }

    [Theory]
    [InlineData("yazıcı")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("INC-2026")]          // eksik biçim
    [InlineData("ABC-2026-00042")]    // tanınmayan tür öneki
    [InlineData("#0")]                // 0 numaralı talep yok
    [InlineData("INC-2026-00000")]
    [InlineData("INC 2026 00042")]
    public void Numara_olmayan_metin_null_doner(string? girdi) =>
        Assert.Null(SearchTerms.ParseTicketReference(girdi));
}

public class SearchRankingTests
{
    private static KnowledgeArticle A(int id, string title, string body, string? category = null) =>
        new()
        {
            Id = id, Title = title, Body = body,
            Status = KnowledgeArticleStatus.Published,
            Category = category == null ? null : new Category { Id = 99, Name = category }
        };

    // Skor ifadesini bellekteki liste üzerinde çalıştırır: üretilen sıra
    private static List<int> Sirala(string sorgu, params KnowledgeArticle[] makaleler)
    {
        var ranked = SearchRanking.Articles(sorgu);
        Assert.NotNull(ranked);

        return makaleler.AsQueryable()
            .Where(ranked!.Matches)
            .OrderByDescending(ranked.Score)
            .ThenBy(a => a.Id)
            .Select(a => a.Id)
            .ToList();
    }

    private static int Skor(string sorgu, KnowledgeArticle makale)
    {
        var ranked = SearchRanking.Articles(sorgu);
        Assert.NotNull(ranked);
        return new[] { makale }.AsQueryable().Select(ranked!.Score).Single();
    }

    [Fact]
    public void Baslik_eslesmesi_govdeden_once_gelir()
    {
        var govde = A(1, "Ağ sorunları", "VPN bağlantısı için istemciyi güncelleyin");
        var baslik = A(2, "VPN bağlantı sorunları", "Bağlantı sık kopuyorsa modemi kontrol edin");

        Assert.Equal(new[] { 2, 1 }, Sirala("vpn", govde, baslik));
    }

    [Fact]
    public void Baslik_kelimeyle_basliyorsa_ek_puan_alir()
    {
        var baslayan = A(1, "Yazıcı kağıt sıkışması giderme", "adımlar");
        var iceren = A(2, "Kat yazıcısı arızası", "adımlar");

        Assert.True(Skor("yazıcı", baslayan) > Skor("yazıcı", iceren));
    }

    [Fact]
    public void Daha_cok_kelime_tutan_makale_one_gecer()
    {
        var tek = A(1, "Kağıt tepsisi ayarı", "tepsi");
        var iki = A(2, "Yazıcı kağıt sıkışması", "tepsi");

        Assert.Equal(new[] { 2, 1 }, Sirala("yazıcı kağıt", tek, iki));
    }

    // Öbek (bitişik ifade) eşleşmesi: iki kelimeyi ayrı ayrı barındıran
    // makaleden önce tam ifadeyi barındıran gelmeli
    [Fact]
    public void Obek_eslesmesi_dagilmis_kelimelerden_degerli()
    {
        var dagilmis = A(1, "Kağıt yükleme ve yazıcı bakımı", "genel");
        var obek = A(2, "Yazıcı kağıt sıkışması", "genel");

        Assert.Equal(new[] { 2, 1 }, Sirala("yazıcı kağıt", dagilmis, obek));
    }

    [Fact]
    public void Kategori_adi_da_aranir()
    {
        var kategoriden = A(1, "Bağlantı kesiliyor", "açıklama", category: "Ağ / İnternet");

        Assert.True(Skor("ağ", kategoriden) > 0);
    }

    [Fact]
    public void Kategorisiz_makale_hata_vermez()
    {
        var kategorisiz = A(1, "VPN", "metin", category: null);

        Assert.True(Skor("vpn", kategorisiz) > 0);
    }

    [Fact]
    public void Eslesmeyen_makale_listeye_girmez()
    {
        var alakasiz = A(1, "Maaş bordrosu", "insan kaynakları");
        var ilgili = A(2, "VPN bağlantısı", "ağ");

        Assert.Equal(new[] { 2 }, Sirala("vpn", alakasiz, ilgili));
    }

    [Fact]
    public void Buyuk_kucuk_harf_farki_sonucu_degistirmez() =>
        Assert.Equal(Skor("vpn", A(1, "VPN Sorunları", "metin")),
                     Skor("VPN", A(1, "vpn sorunları", "metin")));

    [Fact]
    public void Aranacak_kelime_yoksa_siralama_kurulmaz()
    {
        Assert.Null(SearchRanking.Articles(null));
        Assert.Null(SearchRanking.Articles("   "));
        Assert.Null(SearchRanking.Articles("ve ile"));   // yalnızca durak kelime
    }

    // ───── Kaç kelime tuttu (chatbot eşiği) ──────────────────────────────

    [Fact]
    public void Eslesen_kelime_sayisi_alan_sayisindan_bagimsiz()
    {
        // "vpn" hem başlıkta hem gövdede geçiyor: eşleşen KELİME sayısı 1
        var makale = A(1, "VPN sorunu", "vpn bağlantısı kopuyor");
        var ranked = SearchRanking.Build("vpn bağlantı", SearchRanking.ArticleFields, minWordLength: 3);

        Assert.NotNull(ranked);
        var sayi = new[] { makale }.AsQueryable().Select(ranked!.MatchedWordCount).Single();
        Assert.Equal(2, sayi);   // "vpn" ve "bağlantı"
    }

    [Fact]
    public void Esik_altinda_kalan_makale_cevap_sayilmaz()
    {
        var ranked = SearchRanking.Build("vpn bağlantı kopuyor", SearchRanking.ArticleFields, minWordLength: 3);
        Assert.NotNull(ranked);

        var tekKelime = A(1, "VPN kurulumu", "istemciyi indirin");
        var ikiKelime = A(2, "VPN bağlantı sorunları", "istemciyi indirin");

        var gecenler = new[] { tekKelime, ikiKelime }.AsQueryable()
            .Where(ranked!.MatchesAtLeast(2))
            .Select(a => a.Id).ToList();

        Assert.Equal(new[] { 2 }, gecenler);
    }

    // ───── Talep alanları ────────────────────────────────────────────────

    [Fact]
    public void Talep_numarasi_ve_basligi_aranir()
    {
        var ranked = SearchRanking.Tickets("yazıcı");
        Assert.NotNull(ranked);

        var tickets = new[]
        {
            new Ticket { Id = 1, Title = "Monitör titriyor", Number = "INC-2026-00001", RequesterName = "Ayşe" },
            new Ticket { Id = 2, Title = "Yazıcı kağıt sıkıştırıyor", Number = "INC-2026-00002", RequesterName = "Mehmet" }
        };

        Assert.Equal(new[] { 2 }, tickets.AsQueryable()
            .Where(ranked!.Matches).Select(t => t.Id).ToList());
    }

    [Fact]
    public void Numarasi_bos_talep_hata_vermez()
    {
        var ranked = SearchRanking.Tickets("monitör");
        Assert.NotNull(ranked);

        var ticket = new Ticket { Id = 1, Title = "Monitör titriyor", Number = null, RequesterName = "Ayşe" };

        Assert.True(new[] { ticket }.AsQueryable().Select(ranked!.Score).Single() > 0);
    }

    [Fact]
    public void Talep_sahibinin_adi_da_aranir()
    {
        var ranked = SearchRanking.Tickets("mehmet");
        Assert.NotNull(ranked);

        var ticket = new Ticket { Id = 1, Title = "Monitör titriyor", Number = null, RequesterName = "Mehmet Yılmaz" };

        Assert.True(new[] { ticket }.AsQueryable().Select(ranked!.Score).Single() > 0);
    }
}

// Arama sonuçlarının yetki kapsamı: son kullanıcı başkasının talebini
// göremez. Bu kural global arama ucunun tek koruması olduğu için ayrı
// test ediliyor.
public class TicketSearchScopeTests
{
    private static AppUser U(UserRole role, int id, int? teamId = null) =>
        new() { Id = id, FirstName = "T", LastName = id.ToString(), Role = role, TeamId = teamId, IsActive = true };

    private static readonly Ticket[] Tickets =
    {
        new() { Id = 1, RequesterId = 100, AssignedTeamId = 1 },               // son kullanıcının kendi talebi
        new() { Id = 2, RequesterId = 200, AssignedTeamId = 1 },               // başkasının talebi, ekip 1
        new() { Id = 3, RequesterId = 200, AssignedTeamId = 2 },               // başkasının talebi, ekip 2
        new() { Id = 4, RequesterId = 200, AssignedTeamId = 2, AssigneeId = 50 } // ekip 2 ama bana atanmış
    };

    private static List<int> Gorunenler(AppUser user) =>
        Tickets.AsQueryable().SearchableBy(user).Select(t => t.Id).OrderBy(x => x).ToList();

    [Fact]
    public void Son_kullanici_yalnizca_kendi_talebini_gorur() =>
        Assert.Equal(new[] { 1 }, Gorunenler(U(UserRole.User, 100)));

    [Fact]
    public void Baskasinin_talebi_son_kullaniciya_hic_dusmez()
    {
        var sonuc = Gorunenler(U(UserRole.User, 100));

        Assert.DoesNotContain(2, sonuc);
        Assert.DoesNotContain(3, sonuc);
        Assert.DoesNotContain(4, sonuc);
    }

    [Fact]
    public void Ekibi_olan_destek_personeli_ekip_kapsamini_gorur()
    {
        // Ekip 1'deki talepler + kendisine atanan 4 numaralı talep
        var destek = U(UserRole.Agent, 50, teamId: 1);

        Assert.Equal(new[] { 1, 2, 4 }, Gorunenler(destek));
    }

    [Fact]
    public void Ekibi_olmayan_destek_personeli_ve_yonetici_hepsini_gorur()
    {
        Assert.Equal(new[] { 1, 2, 3, 4 }, Gorunenler(U(UserRole.Agent, 50)));
        Assert.Equal(new[] { 1, 2, 3, 4 }, Gorunenler(U(UserRole.Admin, 1)));
    }

    // Nesne tarafı (CanSearch) ile sorgu tarafı (SearchableBy) aynı
    // sonucu vermeli: biri numara eşleşmesinde, diğeri listede kullanılıyor
    [Fact]
    public void Nesne_ve_sorgu_tarafi_ayni_karari_verir()
    {
        foreach (var user in new[] { U(UserRole.User, 100), U(UserRole.Agent, 50, 1), U(UserRole.Agent, 50), U(UserRole.Admin, 1) })
        {
            var sorgudan = Gorunenler(user);
            var kuraldan = Tickets.Where(t => TicketScope.CanSearch(user, t)).Select(t => t.Id).OrderBy(x => x).ToList();

            Assert.Equal(kuraldan, sorgudan);
        }
    }
}
