using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Uygunsuzlukların otomatik açılması (ISO 9001 md. 10.2).
// SLA ihlali ve eşik altı memnuniyet, kalite kaydı doğuran iki olaydır;
// bunlar elle takip edilmeye bırakılmaz.
public class NonconformityService
{
    // Bu puanın altındaki anket ortalaması uygunsuzluk sayılır
    public const double LowSatisfactionThreshold = 2.5;

    // Düzeltici faaliyet için varsayılan termin
    private const int DueDays = 7;

    private readonly AppDbContext _db;
    public NonconformityService(AppDbContext db) => _db = db;

    // Aynı talep ve kaynak için ikinci kayıt açılmaz
    private Task<bool> ExistsAsync(int ticketId, NonconformitySource source) =>
        _db.Nonconformities.AnyAsync(n => n.TicketId == ticketId && n.Source == source);

    // SaveChanges çağırmaz; çağıran kendi işlemiyle birlikte kaydeder
    public async Task<bool> QueueForSlaBreachAsync(Ticket ticket)
    {
        if (await ExistsAsync(ticket.Id, NonconformitySource.SlaBreach)) return false;

        _db.Nonconformities.Add(new Nonconformity
        {
            TicketId = ticket.Id,
            Source = NonconformitySource.SlaBreach,
            Status = NonconformityStatus.Open,
            Description = $"{Ui.TicketNo(ticket)} talebinde hizmet seviyesi aşıldı: "
                        + $"hedef {AppTime.ToLocal(ticket.SlaDueAt):dd.MM.yyyy HH:mm}, çözüm zamanında tamamlanmadı.",
            ResponsibleId = ticket.AssigneeId,
            DetectedAt = AppTime.UtcNow,
            DueAt = AppTime.UtcNow.AddDays(DueDays)
        });
        return true;
    }

    public async Task<bool> QueueForLowSatisfactionAsync(Ticket ticket, double average, string? comment)
    {
        if (average >= LowSatisfactionThreshold) return false;
        if (await ExistsAsync(ticket.Id, NonconformitySource.LowSatisfaction)) return false;

        var ek = string.IsNullOrWhiteSpace(comment) ? "" : $" Kullanıcı yorumu: \"{comment}\"";

        _db.Nonconformities.Add(new Nonconformity
        {
            TicketId = ticket.Id,
            Source = NonconformitySource.LowSatisfaction,
            Status = NonconformityStatus.Open,
            Description = $"{Ui.TicketNo(ticket)} talebinde memnuniyet ortalaması {average:0.0}/5 "
                        + $"({LowSatisfactionThreshold} eşiğinin altında).{ek}",
            ResponsibleId = ticket.AssigneeId,
            DetectedAt = AppTime.UtcNow,
            DueAt = AppTime.UtcNow.AddDays(DueDays)
        });
        return true;
    }
}
