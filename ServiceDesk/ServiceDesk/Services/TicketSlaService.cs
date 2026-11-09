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

        var (calendar, holidays) = await LoadCalendarAsync(policy, ticket.RequesterId);

        ticket.FirstResponseDueAt =
            SlaCalculator.AddWorkingMinutes(ticket.CreatedAt, policy.FirstResponseMinutes, calendar, holidays);

        ticket.SlaDueAt =
            SlaCalculator.AddWorkingMinutes(ticket.CreatedAt, policy.ResolutionMinutes, calendar, holidays);
    }

    // Öncelik değişince: yeni önceliğin politikası (talebin açıldığı anda
    // yürürlükte olan sürüm) bulunur, hedefler açılış anından yeniden
    // hesaplanır. Daha önce "Müşteri bekleniyor"da geçen süre hedefe eklenir.
    // Kayıtlı ihlal işaretleri silinmez (denetim izi).
    public async Task RecalculateAsync(Ticket ticket)
    {
        var policy = await ResolvePolicyAsync(ticket);
        if (policy == null) return;

        ticket.SlaPolicyId = policy.Id;
        var (calendar, holidays) = await LoadCalendarAsync(policy, ticket.RequesterId);

        if (ticket.FirstRespondedAt == null)
            ticket.FirstResponseDueAt = SlaCalculator.AddWorkingMinutes(
                ticket.CreatedAt, policy.FirstResponseMinutes + ticket.SlaPausedMinutes, calendar, holidays);

        ticket.SlaDueAt = SlaCalculator.AddWorkingMinutes(
            ticket.CreatedAt, policy.ResolutionMinutes + ticket.SlaPausedMinutes, calendar, holidays);

        // Yeni hedefe göre uyarı tekrar gönderilebilsin
        if (!ticket.IsSlaBreached) ticket.SlaWarningSentAt = null;
    }

    // Durum değişikliğinin SLA'lı yolu: "Müşteri bekleniyor"dan çıkılıyorsa
    // duraklanan MESAİ süresi kadar hedefler ileri kaydırılır, sonra geçiş uygulanır.
    public async Task ApplyStatusAsync(Ticket ticket, TicketStatus target, DateTime nowUtc)
    {
        if (ticket.SlaPausedAt is { } pausedAt && target != TicketStatus.WaitingForCustomer)
        {
            ticket.SlaPausedAt = null;

            var policy = ticket.SlaPolicyId == null
                ? null
                : await _db.SlaPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == ticket.SlaPolicyId);
            var (calendar, holidays) = await LoadCalendarAsync(policy, ticket.RequesterId);

            var paused = SlaCalculator.WorkingMinutesBetween(pausedAt, nowUtc, calendar, holidays);
            if (paused > 0)
            {
                ticket.SlaPausedMinutes += paused;
                ticket.SlaDueAt = SlaCalculator.AddWorkingMinutes(ticket.SlaDueAt, paused, calendar, holidays);
                if (ticket.FirstRespondedAt == null && ticket.FirstResponseDueAt != null)
                    ticket.FirstResponseDueAt = SlaCalculator.AddWorkingMinutes(
                        ticket.FirstResponseDueAt.Value, paused, calendar, holidays);
            }
        }

        TicketWorkflow.Apply(ticket, target, nowUtc);
    }

    // Takvim seçimi: politika 7x24 ise takvim yok; mesai saatliyse önce talep
    // edenin biriminin takvimi, yoksa politikanın takvimi.
    private async Task<(BusinessCalendar?, IReadOnlySet<DateTime>?)> LoadCalendarAsync(SlaPolicy? policy, int requesterId)
    {
        if (policy == null || !policy.BusinessHoursOnly) return (null, null);

        var departmentCalendarId = await _db.Users.AsNoTracking()
            .Where(u => u.Id == requesterId && u.Department != null)
            .Select(u => u.Department!.BusinessCalendarId)
            .FirstOrDefaultAsync();

        var calendarId = departmentCalendarId ?? policy.BusinessCalendarId;
        if (calendarId == null) return (null, null);

        var calendar = await _db.BusinessCalendars.AsNoTracking().FirstOrDefaultAsync(c => c.Id == calendarId);
        if (calendar == null) return (null, null);

        IReadOnlySet<DateTime> holidays = (await _db.Holidays.AsNoTracking()
                .Where(h => h.BusinessCalendarId == calendarId)
                .Select(h => h.Date)
                .ToListAsync())
            .Select(d => d.Date)
            .ToHashSet();

        return (calendar, holidays);
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
