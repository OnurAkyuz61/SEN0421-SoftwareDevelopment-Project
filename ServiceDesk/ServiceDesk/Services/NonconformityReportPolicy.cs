using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Rapora giren tek uygunsuzluk satırı. Entity değil projeksiyon: rapor
// sorgusu yalnızca ihtiyaç duyduğu kolonları okur.
public sealed record NonconformityReportRow(
    int Id,
    NonconformitySource Source,
    NonconformityStatus Status,
    DateTime DetectedAt,
    DateTime? DueAt,
    DateTime? ClosedAt,
    bool Verified,
    int EvidenceCount,
    string? Responsible,
    int? TicketId,
    string? TicketNo,
    string Description)
{
    // Kapanış süresi (gün): tespitten kapanışa. Açık kayıtta yok.
    public double? ClosureDays => ClosedAt == null
        ? null
        : Math.Max(0, (ClosedAt.Value - DetectedAt).TotalDays);

    // Termin uyumu yalnızca TERMİNİ OLAN ve KAPANAN kayıtta sorulabilir:
    //   true  → termininde (ya da öncesinde) kapandı
    //   false → terminden sonra kapandı
    //   null  → kapanmadı ya da termini yok (ölçüye girmez)
    public bool? ClosedOnTime => ClosedAt == null || DueAt == null ? null : ClosedAt <= DueAt;
}

// Kaynak bazlı kırılım: hangi olay türü kaç uygunsuzluk doğuruyor, ne kadar
// sürede kapanıyor, termininde kapanıyor mu.
public sealed record NonconformitySourceStat(
    NonconformitySource? Source,     // null = TOPLAM satırı
    int Total, int Open, int InProgress, int Closed,
    double? AvgClosureDays,
    int ClosedOnTime, int ClosedLate, int OverdueOpen, int Verified)
{
    // Termin uyumu, termini olan ve kapanan kayıtlar üzerinden
    public int OnTimeMeasured => ClosedOnTime + ClosedLate;

    public double? OnTimeRate => OnTimeMeasured == 0 ? null : 100.0 * ClosedOnTime / OnTimeMeasured;

    public double ClosedRate => Total == 0 ? 0 : 100.0 * Closed / Total;

    // Etkinlik doğrulama oranı: KAPANAN kayıtların kaçında doğrulama kaydı var
    // (md. 10.2.1 d). Kapanış artık doğrulama olmadan mümkün değil, ama bu
    // geçişten ÖNCE kapanmış kayıtlar oranı düşürür — yönetim gözden
    // geçirmesinde görülmesi gereken şey tam olarak bu.
    //
    // Hiç kapanmamış kaynakta null: "%0 doğrulanmış" ile "doğrulanacak kayıt
    // yok" aynı şey değildir. Doğrulama kapanmamış kayıtta da işaretlenebildiği
    // için oran %100'de sınırlanır.
    public double? VerificationRate => Closed == 0 ? null : Math.Min(100.0, 100.0 * Verified / Closed);
}

// Kaynak bazlı uygunsuzluk raporunun hesabı (ISO 9001 md. 9.1.3: analiz ve
// değerlendirme; md. 10.2: düzeltici faaliyetin etkinliği).
//
// Toplamlar SQL'de GROUP BY ile de çıkarılabilirdi; bellekte yapılıyor çünkü
// "kapanış süresi" ve "termin uyumu" tanımı tek yerde kalsın: aynı kural bir
// de SQL'e yazılsaydı ortalamanın yuvarlanmasında ve termini olmayan kaydın
// ölçüye girip girmediğinde iki taraf farklı karar verirdi. Uygunsuzluk
// tablosu yüz binlerce satırlık bir tablo değil; dönem süzgeciyle okunan
// küçük bir projeksiyon bu rapor için yeterli (KnowledgeUsage raporuyla aynı
// gerekçe).
//
// Saf sınıf: veritabanına ve HTTP'ye bağımlı değil, birim testi yazılabiliyor.
public static class NonconformityReportPolicy
{
    // Kaynak başına kırılım + sonda TOPLAM satırı. Kaydı olmayan kaynak da
    // listede kalır (sıfırla): "SLA ihlali hiç uygunsuzluk doğurmamış"
    // bilgisi, satırın hiç görünmemesinden farklı bir şeydir.
    public static List<NonconformitySourceStat> Summarize(
        IReadOnlyCollection<NonconformityReportRow> rows, DateTime nowUtc)
    {
        var list = Enum.GetValues<NonconformitySource>()
            .Select(src => Build(src, rows.Where(r => r.Source == src).ToList(), nowUtc))
            .ToList();

        list.Add(Build(null, rows, nowUtc));
        return list;
    }

    private static NonconformitySourceStat Build(
        NonconformitySource? source, IReadOnlyCollection<NonconformityReportRow> rows, DateTime nowUtc)
    {
        var kapananlar = rows.Where(r => r.ClosureDays != null).Select(r => r.ClosureDays!.Value).ToList();

        return new NonconformitySourceStat(
            Source: source,
            Total: rows.Count,
            Open: rows.Count(r => r.Status == NonconformityStatus.Open),
            InProgress: rows.Count(r => r.Status == NonconformityStatus.InProgress),
            Closed: rows.Count(r => r.Status == NonconformityStatus.Closed),
            AvgClosureDays: kapananlar.Count == 0 ? null : kapananlar.Average(),
            ClosedOnTime: rows.Count(r => r.ClosedOnTime == true),
            ClosedLate: rows.Count(r => r.ClosedOnTime == false),
            // Açık ve termini geçmiş: entity'deki IsOverdue ile aynı kural
            OverdueOpen: rows.Count(r => IsOverdue(r, nowUtc)),
            Verified: rows.Count(r => r.Verified));
    }

    // Nonconformity.IsOverdue'nun satır karşılığı: kapanmamış ve termini geçmiş
    public static bool IsOverdue(NonconformityReportRow r, DateTime nowUtc) =>
        r.Status != NonconformityStatus.Closed && r.DueAt != null && nowUtc > r.DueAt;
}
