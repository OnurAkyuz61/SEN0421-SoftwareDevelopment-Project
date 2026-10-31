using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Aynı talebi iki kişinin aynı anda düzenlemesi (iyimser eşzamanlılık).
//
// Talep sayfası açıldığında talebin o anki sürümü (Ticket.RowVersion) her
// forma gizli alan olarak yazılır. Form gönderildiğinde:
//   1. Gelen sürüm veritabanındakiyle karşılaştırılır. Arada başkası talebi
//      değiştirdiyse işlem hiç uygulanmaz, kullanıcı güncel sayfaya döner
//      ve kimin ne zaman değiştirdiğini görür.
//   2. Karşılaştırma ile kayıt arasında geçen milisaniyelerde gelen bir
//      değişikliğe karşı, gelen sürüm EF'e "beklenen sürüm" olarak verilir:
//      UPDATE ... WHERE RowVersion = @beklenen. Satır bulunamazsa EF
//      DbUpdateConcurrencyException fırlatır, yine aynı uyarı gösterilir.
//
// Böylece "son kaydeden kazanır" durumu ortadan kalkar: ikinci kişi, birincinin
// değişikliğini görmeden üzerine yazamaz.
public static class TicketConcurrency
{
    public const string FieldName = "rowVersion";

    public static string? Encode(byte[]? version) =>
        version is { Length: > 0 } ? Convert.ToBase64String(version) : null;

    public static byte[]? Decode(string? posted)
    {
        if (string.IsNullOrWhiteSpace(posted)) return null;
        try { return Convert.FromBase64String(posted); }
        catch (FormatException) { return Array.Empty<byte>(); } // bozuk değer: eşleşmez, çakışma sayılır
    }

    // Saf karşılaştırma (birim testi için). Sürüm gönderilmediyse kontrol
    // yapılmaz: talep sayfası dışından gelen istekler (ör. atama listesinden
    // "Ata") eski davranışını korur.
    public static bool IsStale(byte[]? current, byte[]? posted) =>
        posted != null && (current == null || !current.AsSpan().SequenceEqual(posted));

    // Ticket izlenen (tracked) bir varlık olmalı. Dönüş: sürüm eskiyse true.
    // Eski değilse beklenen sürüm EF'e verilir (yukarıdaki 2. madde).
    public static bool Expect(AppDbContext db, Ticket ticket, string? postedVersion)
    {
        var posted = Decode(postedVersion);
        if (IsStale(ticket.RowVersion, posted)) return true;
        if (posted != null) db.Entry(ticket).Property(t => t.RowVersion).OriginalValue = posted;
        return false;
    }

    // Talebin kendi kolonları değişmeyen işlemler (ör. değişiklik planı ayrı
    // tabloda) yine de talebin sürümünü ilerletmeli; yoksa bu işlem diğer
    // kullanıcıya görünmez ve çakışma kontrolü devreye girmez. Değeri aynı
    // kalan bir kolon "değişti" işaretlenir: UPDATE yine sürüm kontrolüyle
    // çalışır ve RowVersion yenilenir. Geçmiş kaydına iz düşmez.
    public static void Touch(AppDbContext db, Ticket ticket) =>
        db.Entry(ticket).Property(t => t.Title).IsModified = true;

    // Kullanıcıya gösterilecek açıklama: kim, ne zaman, ne değiştirdi.
    public static async Task<string> ConflictMessageAsync(AppDbContext db, int ticketId, int? meId)
    {
        var son = await db.TicketHistories.AsNoTracking()
            .Include(h => h.ChangedBy)
            .Where(h => h.TicketId == ticketId)
            .OrderByDescending(h => h.ChangedAt).ThenByDescending(h => h.Id)
            .FirstOrDefaultAsync();

        var kim = son == null ? null
            : son.ChangedById == null ? "sistem (SLA izleme)"
            : son.ChangedById == meId ? "siz (başka bir sekmede)"
            : son.ChangedBy?.FullName;

        var ayrinti = son == null || kim == null
            ? ""
            : $" Son değişiklik: {kim}, {AppTime.ToLocal(son.ChangedAt):dd.MM.yyyy HH:mm} · {son.Field}: " +
              $"{son.OldValue ?? "-"} → {son.NewValue ?? "-"}.";

        return "Bu talep, siz sayfayı açtıktan sonra başka biri tarafından güncellendi; " +
               "değişikliğiniz uygulanmadı." + ayrinti +
               " Sayfa en güncel haliyle yenilendi, kontrol edip işleminizi tekrarlayın.";
    }

    // Kayıt anında yakalanan çakışma: izlenen değişiklikler atılır ki aynı
    // istek içinde başka bir SaveChanges onları tekrar denemesin.
    public static void DiscardChanges(AppDbContext db)
    {
        foreach (var e in db.ChangeTracker.Entries().ToList())
            e.State = EntityState.Detached;
    }
}
