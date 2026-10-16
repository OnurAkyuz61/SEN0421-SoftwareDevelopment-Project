using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Talebe uygulanacak SLA politikasını seçer ve hedef tarihleri yazar.
// Tek giriş noktası: talep nereden açılırsa açılsın (portal, ileride API veya
// toplu içe aktarma) kural aynı yerden uygulanır.
public class TicketSlaService
{
    private readonly AppDbContext _db;
    public TicketSlaService(AppDbContext db) => _db = db;

    public async Task ApplyPolicyAsync(Ticket ticket)
    {
        var policy = await ResolvePolicyAsync(ticket);
        if (policy == null)
        {
            // Politika bulunamazsa talep yine de açılır; hedef 24 saat sonrası olur
            ticket.SlaDueAt = ticket.CreatedAt.AddHours(24);
            return;
        }

        ticket.SlaPolicyId = policy.Id;

        BusinessCalendar? calendar = null;
        IReadOnlySet<DateTime>? holidays = null;

        if (policy.BusinessHoursOnly && policy.BusinessCalendarId != null)
        {
            calendar = await _db.BusinessCalendars
                .FirstOrDefaultAsync(c => c.Id == policy.BusinessCalendarId);

            holidays = (await _db.Holidays
                    .Where(h => h.BusinessCalendarId == policy.BusinessCalendarId)
                    .Select(h => h.Date)
                    .ToListAsync())
                .Select(d => d.Date)
                .ToHashSet();
        }

        ticket.FirstResponseDueAt =
            SlaCalculator.AddWorkingMinutes(ticket.CreatedAt, policy.FirstResponseMinutes, calendar, holidays);

        ticket.SlaDueAt =
            SlaCalculator.AddWorkingMinutes(ticket.CreatedAt, policy.ResolutionMinutes, calendar, holidays);
    }

    // Önceliğe uyan, yürürlükteki en özel politika: tür belirtilmiş olan,
    // genel olana tercih edilir
    private Task<SlaPolicy?> ResolvePolicyAsync(Ticket ticket)
    {
        var at = ticket.CreatedAt;

        return _db.SlaPolicies
            .Where(p => p.IsActive
                        && p.Priority == ticket.Priority
                        && (p.AppliesToType == null || p.AppliesToType == ticket.Type)
                        && p.EffectiveFrom <= at
                        && (p.EffectiveTo == null || p.EffectiveTo > at))
            .OrderByDescending(p => p.AppliesToType != null)
            .ThenByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync();
    }
}
