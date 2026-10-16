using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Bildirimler veritabanında tutulmaz, talep verisinden anlık hesaplanır
public static class NotificationService
{
    public static async Task<List<NotificationItem>> GetAsync(AppDbContext db)
    {
        var now = AppTime.UtcNow;
        var list = new List<NotificationItem>();

        var open = await db.Tickets
            .Where(t => t.Status == TicketStatus.New || t.Status == TicketStatus.InProgress)
            .ToListAsync();

        foreach (var t in open)
        {
            if (t.SlaDueAt < now)
                list.Add(new(t.Id, $"SLA ihlali: #{t.Id} {t.Title}", "Çözüm süresi aşıldı, eskalasyon gerekli.", "danger", "bi-exclamation-octagon", t.SlaDueAt));
            else if (t.SlaDueAt < now.AddHours(2))
                list.Add(new(t.Id, $"SLA yaklaşıyor: #{t.Id} {t.Title}", $"Kalan süre: {(t.SlaDueAt - now).TotalMinutes:0} dk", "warning", "bi-hourglass-split", t.SlaDueAt));

            if (t.ApprovalStatus == ApprovalStatus.Pending)
                list.Add(new(t.Id, $"Onay bekliyor: #{t.Id} {t.Title}", $"{t.RequesterName} bir değişiklik talep etti.", "warning", "bi-patch-question", t.CreatedAt));

            if (t.CreatedAt > now.AddHours(-24))
                list.Add(new(t.Id, $"Yeni talep: #{t.Id} {t.Title}", $"{t.RequesterName} tarafından açıldı.", "primary", "bi-plus-circle", t.CreatedAt));
        }

        // Önce ihlaller, sonra yaklaşanlar, sonra yeni talepler
        return list.OrderBy(n => n.Color == "danger" ? 0 : n.Color == "warning" ? 1 : 2)
                   .ThenByDescending(n => n.Time).ToList();
    }
}

public record NotificationItem(int TicketId, string Title, string Message, string Color, string Icon, DateTime Time);
