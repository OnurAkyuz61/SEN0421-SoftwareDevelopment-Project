using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Ekip açık talep kotası (saf kurallar + DB sayımı).
//
// Kota Team.MaxOpenTickets ile tanımlanır; null = sınırsız.
// Sayılan talepler: AssignedTeamId eşleşen ve TicketWorkflow.IsOpen durumundaki
// kayıtlar. Kota doluysa yeni talep o ekibe düşürülemez (portal açılışı).
public static class TeamCapacityPolicy
{
    // maxOpen null veya geçersiz → sınır yok. currentOpen mevcut açık sayı;
    // adding eklenecek talep (genelde 1).
    public static string? Error(string teamName, int? maxOpen, int currentOpen, int adding = 1)
    {
        if (maxOpen is null or < 1) return null;
        if (adding < 1) adding = 1;
        if (currentOpen + adding <= maxOpen.Value) return null;

        return $"\"{teamName}\" ekibinin açık talep kotası dolu ({currentOpen}/{maxOpen}). " +
               "Başka bir kategori seçin veya mevcut talepler kapansın.";
    }

    // Katalog kaydı: boş / 0 → sınırsız (null). Negatif reddedilir.
    public static string? ValidateLimit(int? maxOpen) =>
        maxOpen is < 0 ? "Açık talep kotası negatif olamaz."
        : maxOpen is 0 ? "Kota 0 olamaz; sınırsız için alanı boş bırakın."
        : null;
}

public class TeamCapacity
{
    private readonly AppDbContext _db;
    public TeamCapacity(AppDbContext db) => _db = db;

    // Ekibe bir açık talep daha eklenebilir mi? excludeTicketId aynı talebin
    // yeniden sayılmasını engeller (ileride ekip değişiminde kullanılır).
    public async Task<string?> CheckAsync(int teamId, int? excludeTicketId = null)
    {
        var team = await _db.Teams.AsNoTracking()
            .Where(t => t.Id == teamId)
            .Select(t => new { t.Name, t.MaxOpenTickets })
            .FirstOrDefaultAsync();
        if (team == null) return "Geçerli bir ekip seçin.";
        if (team.MaxOpenTickets is null or < 1) return null;

        var open = await CountOpenAsync(teamId, excludeTicketId);
        return TeamCapacityPolicy.Error(team.Name, team.MaxOpenTickets, open);
    }

    public Task<int> CountOpenAsync(int teamId, int? excludeTicketId = null)
    {
        var q = _db.Tickets.AsNoTracking()
            .Where(t => t.AssignedTeamId == teamId
                && (t.Status == TicketStatus.New
                    || t.Status == TicketStatus.InProgress
                    || t.Status == TicketStatus.WaitingForCustomer));
        if (excludeTicketId != null)
            q = q.Where(t => t.Id != excludeTicketId.Value);
        return q.CountAsync();
    }
}
