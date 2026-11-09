using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// SLA yönetim API'si (yalnızca yönetici). JSON alır / döner.
//
//   SLA politikaları  GET/POST      api/sla/policies
//                     GET/PUT/DEL   api/sla/policies/{id}
//                     GET           api/sla/policies/{id}/versions
//   Mesai takvimleri  GET/POST      api/sla/calendars
//                     GET/PUT/DEL   api/sla/calendars/{id}
//   Resmî tatiller    GET/POST      api/sla/calendars/{id}/holidays
//                     DELETE        api/sla/holidays/{id}
//   Birim takvimi     GET           api/sla/departments
//                     PUT           api/sla/departments/{id}/calendar
//
// Politika güncellemesi kaydı ezmez, yeni SÜRÜM açar: eski sürüm o andan
// itibaren yürürlükten kalkar (EffectiveTo), mevcut talepler bağlı oldukları
// sürümle ölçülmeye devam eder.
//
// Tüm yazma uçları JSON gövde / DELETE ile çalışır; tarayıcı bunları başka bir
// siteden ön kontrol (CORS) olmadan gönderemez.
[ApiController]
[Route("api/sla")]
[AuthorizeRole(UserRole.Admin)]
public class SlaAdminController : ControllerBase
{
    private const int MaxMinutes = 365 * 24 * 60;

    private readonly AppDbContext _db;
    public SlaAdminController(AppDbContext db) => _db = db;

    public record SlaPolicyInput(
        string? Name, Priority Priority, TicketType? AppliesToType,
        int FirstResponseMinutes, int ResolutionMinutes,
        bool BusinessHoursOnly, int? BusinessCalendarId,
        int WarningMinutesBefore = 120, bool IsActive = true);

    public record CalendarInput(string? Name, int WorkStartMinutes, int WorkEndMinutes, string? WorkDays);
    public record HolidayInput(DateTime Date, string? Name);
    public record DepartmentCalendarInput(int? CalendarId);

    // ───── SLA politikaları ───────────────────────────────────────────────

    // all=false: yalnızca yürürlükteki sürümler; all=true: tüm sürümler
    [HttpGet("policies")]
    public async Task<IActionResult> GetPolicies(bool all = false)
    {
        var now = AppTime.UtcNow;
        var q = _db.SlaPolicies.AsNoTracking();
        if (!all) q = q.Where(p => p.IsActive && (p.EffectiveTo == null || p.EffectiveTo > now));

        var list = await q.OrderBy(p => p.Priority).ThenBy(p => p.Name).ThenByDescending(p => p.Version).ToListAsync();
        return Ok(list.Select(ToDto));
    }

    [HttpGet("policies/{id:int}")]
    public async Task<IActionResult> GetPolicy(int id)
    {
        var p = await _db.SlaPolicies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return p == null ? NotFound() : Ok(ToDto(p));
    }

    // Aynı kökten gelen tüm sürümler (en yeni üstte)
    [HttpGet("policies/{id:int}/versions")]
    public async Task<IActionResult> GetVersions(int id)
    {
        var p = await _db.SlaPolicies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();

        var root = p.RootPolicyId ?? p.Id;
        var versions = await _db.SlaPolicies.AsNoTracking()
            .Where(x => x.Id == root || x.RootPolicyId == root)
            .OrderByDescending(x => x.Version)
            .ToListAsync();
        return Ok(versions.Select(ToDto));
    }

    [HttpPost("policies")]
    public async Task<IActionResult> CreatePolicy(SlaPolicyInput input)
    {
        if (await ValidatePolicyAsync(input) is { } hata) return BadRequest(new { error = hata });

        var p = new SlaPolicy { EffectiveFrom = AppTime.UtcNow, Version = 1 };
        Fill(p, input);
        _db.SlaPolicies.Add(p);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPolicy), new { id = p.Id }, ToDto(p));
    }

    // Güncelleme = yeni sürüm
    [HttpPut("policies/{id:int}")]
    public async Task<IActionResult> UpdatePolicy(int id, SlaPolicyInput input)
    {
        var old = await _db.SlaPolicies.FirstOrDefaultAsync(x => x.Id == id);
        if (old == null) return NotFound();

        var now = AppTime.UtcNow;
        if (old.EffectiveTo != null && old.EffectiveTo <= now)
            return Conflict(new { error = "Bu sürüm artık yürürlükte değil; güncel sürümü düzenleyin." });
        if (await ValidatePolicyAsync(input) is { } hata) return BadRequest(new { error = hata });

        old.EffectiveTo = now;

        var yeni = new SlaPolicy
        {
            EffectiveFrom = now,
            Version = old.Version + 1,
            RootPolicyId = old.RootPolicyId ?? old.Id
        };
        Fill(yeni, input);
        _db.SlaPolicies.Add(yeni);
        await _db.SaveChangesAsync();

        return Ok(ToDto(yeni));
    }

    // Silme yerine pasifleştirme: talepler politikaya bağlı (Restrict)
    [HttpDelete("policies/{id:int}")]
    public async Task<IActionResult> DeletePolicy(int id)
    {
        var p = await _db.SlaPolicies.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();

        p.IsActive = false;
        p.EffectiveTo ??= AppTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string?> ValidatePolicyAsync(SlaPolicyInput i)
    {
        var name = i.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 100) return "Politika adı 1–100 karakter olmalı.";
        if (!Enum.IsDefined(i.Priority)) return "Geçersiz öncelik.";
        if (i.AppliesToType != null && !Enum.IsDefined(i.AppliesToType.Value)) return "Geçersiz talep türü.";
        if (i.FirstResponseMinutes < 1 || i.FirstResponseMinutes > MaxMinutes) return "İlk yanıt süresi geçersiz.";
        if (i.ResolutionMinutes < i.FirstResponseMinutes || i.ResolutionMinutes > MaxMinutes)
            return "Çözüm süresi ilk yanıt süresinden kısa olamaz.";
        if (i.WarningMinutesBefore < 1 || i.WarningMinutesBefore > i.ResolutionMinutes)
            return "Uyarı eşiği 1 dakika ile çözüm süresi arasında olmalı.";
        if (i.BusinessCalendarId != null && !await _db.BusinessCalendars.AnyAsync(c => c.Id == i.BusinessCalendarId))
            return "Mesai takvimi bulunamadı.";
        return null;
    }

    private static void Fill(SlaPolicy p, SlaPolicyInput i)
    {
        p.Name = i.Name!.Trim();
        p.Priority = i.Priority;
        p.AppliesToType = i.AppliesToType;
        p.FirstResponseMinutes = i.FirstResponseMinutes;
        p.ResolutionMinutes = i.ResolutionMinutes;
        p.BusinessHoursOnly = i.BusinessHoursOnly;
        p.BusinessCalendarId = i.BusinessCalendarId;
        p.WarningMinutesBefore = i.WarningMinutesBefore;
        p.IsActive = i.IsActive;
    }

    private static object ToDto(SlaPolicy p) => new
    {
        p.Id, p.Name, p.Priority, priorityText = Ui.PriorityText(p.Priority), p.AppliesToType,
        p.FirstResponseMinutes, p.ResolutionMinutes, p.WarningMinutesBefore,
        p.BusinessHoursOnly, p.BusinessCalendarId,
        p.Version, rootPolicyId = p.RootPolicyId ?? p.Id,
        p.EffectiveFrom, p.EffectiveTo, p.IsActive
    };

    // ───── Mesai takvimleri ───────────────────────────────────────────────

    [HttpGet("calendars")]
    public async Task<IActionResult> GetCalendars()
    {
        var list = await _db.BusinessCalendars.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.WorkStartMinutes, c.WorkEndMinutes, c.WorkDays, holidayCount = c.Holidays.Count })
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("calendars/{id:int}")]
    public async Task<IActionResult> GetCalendar(int id)
    {
        var c = await _db.BusinessCalendars.AsNoTracking().Include(x => x.Holidays).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        return Ok(new
        {
            c.Id, c.Name, c.WorkStartMinutes, c.WorkEndMinutes, c.WorkDays,
            holidays = c.Holidays.OrderBy(h => h.Date).Select(h => new { h.Id, date = h.Date.ToString("yyyy-MM-dd"), h.Name })
        });
    }

    [HttpPost("calendars")]
    public async Task<IActionResult> CreateCalendar(CalendarInput input)
    {
        var (days, hata) = ValidateCalendar(input);
        if (hata != null) return BadRequest(new { error = hata });

        var c = new BusinessCalendar
        {
            Name = input.Name!.Trim(), WorkStartMinutes = input.WorkStartMinutes,
            WorkEndMinutes = input.WorkEndMinutes, WorkDays = days!
        };
        _db.BusinessCalendars.Add(c);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCalendar), new { id = c.Id }, new { c.Id, c.Name, c.WorkStartMinutes, c.WorkEndMinutes, c.WorkDays });
    }

    // Takvim değişikliği yalnızca bundan sonra hesaplanan hedefleri etkiler
    [HttpPut("calendars/{id:int}")]
    public async Task<IActionResult> UpdateCalendar(int id, CalendarInput input)
    {
        var c = await _db.BusinessCalendars.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

        var (days, hata) = ValidateCalendar(input);
        if (hata != null) return BadRequest(new { error = hata });

        c.Name = input.Name!.Trim();
        c.WorkStartMinutes = input.WorkStartMinutes;
        c.WorkEndMinutes = input.WorkEndMinutes;
        c.WorkDays = days!;
        await _db.SaveChangesAsync();
        return Ok(new { c.Id, c.Name, c.WorkStartMinutes, c.WorkEndMinutes, c.WorkDays });
    }

    [HttpDelete("calendars/{id:int}")]
    public async Task<IActionResult> DeleteCalendar(int id)
    {
        var c = await _db.BusinessCalendars.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

        if (await _db.SlaPolicies.AnyAsync(p => p.BusinessCalendarId == id)
            || await _db.Departments.AnyAsync(d => d.BusinessCalendarId == id))
            return Conflict(new { error = "Takvim bir SLA politikasında veya birimde kullanılıyor; önce bağlantıyı kaldırın." });

        _db.BusinessCalendars.Remove(c);   // tatiller cascade ile silinir
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static (string? Days, string? Error) ValidateCalendar(CalendarInput i)
    {
        var name = i.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 100) return (null, "Takvim adı 1–100 karakter olmalı.");
        if (i.WorkStartMinutes < 0 || i.WorkEndMinutes > 24 * 60 || i.WorkStartMinutes >= i.WorkEndMinutes)
            return (null, "Mesai başlangıcı bitişten önce olmalı (dakika cinsinden, 0–1440).");

        var parts = (i.WorkDays ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var days = new SortedSet<int>();
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var d) || d < 1 || d > 7)
                return (null, "Çalışma günleri 1–7 arası sayılar olmalı (1 = Pazartesi), ör. \"1,2,3,4,5\".");
            days.Add(d);
        }
        if (days.Count == 0) return (null, "En az bir çalışma günü seçin.");

        return (string.Join(",", days), null);
    }

    // ───── Resmî tatiller ─────────────────────────────────────────────────

    [HttpGet("calendars/{id:int}/holidays")]
    public async Task<IActionResult> GetHolidays(int id, int? year = null)
    {
        if (!await _db.BusinessCalendars.AnyAsync(c => c.Id == id)) return NotFound();

        var q = _db.Holidays.AsNoTracking().Where(h => h.BusinessCalendarId == id);
        if (year != null) q = q.Where(h => h.Date.Year == year);

        var list = await q.OrderBy(h => h.Date).ToListAsync();
        return Ok(list.Select(h => new { h.Id, date = h.Date.ToString("yyyy-MM-dd"), h.Name }));
    }

    [HttpPost("calendars/{id:int}/holidays")]
    public async Task<IActionResult> AddHoliday(int id, HolidayInput input)
    {
        if (!await _db.BusinessCalendars.AnyAsync(c => c.Id == id)) return NotFound();

        var name = input.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 100) return BadRequest(new { error = "Tatil adı 1–100 karakter olmalı." });
        if (input.Date == default) return BadRequest(new { error = "Geçerli bir tarih girin (yyyy-MM-dd)." });

        var date = input.Date.Date;
        if (await _db.Holidays.AnyAsync(h => h.BusinessCalendarId == id && h.Date == date))
            return Conflict(new { error = "Bu tarihte zaten bir tatil tanımlı." });

        var h = new Holiday { BusinessCalendarId = id, Date = date, Name = name };
        _db.Holidays.Add(h);
        await _db.SaveChangesAsync();
        return Ok(new { h.Id, date = h.Date.ToString("yyyy-MM-dd"), h.Name });
    }

    [HttpDelete("holidays/{id:int}")]
    public async Task<IActionResult> DeleteHoliday(int id)
    {
        var h = await _db.Holidays.FirstOrDefaultAsync(x => x.Id == id);
        if (h == null) return NotFound();

        _db.Holidays.Remove(h);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ───── Birim bazlı iş takvimi ─────────────────────────────────────────

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var list = await _db.Departments.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new
            {
                d.Id, d.Name, d.IsActive, d.BusinessCalendarId,
                calendarName = d.BusinessCalendar != null ? d.BusinessCalendar.Name : null
            })
            .ToListAsync();
        return Ok(list);
    }

    // calendarId boş gönderilirse birim politikanın takvimine döner
    [HttpPut("departments/{id:int}/calendar")]
    public async Task<IActionResult> SetDepartmentCalendar(int id, DepartmentCalendarInput input)
    {
        var d = await _db.Departments.FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return NotFound();

        if (input.CalendarId != null && !await _db.BusinessCalendars.AnyAsync(c => c.Id == input.CalendarId))
            return BadRequest(new { error = "Mesai takvimi bulunamadı." });

        d.BusinessCalendarId = input.CalendarId;
        await _db.SaveChangesAsync();
        return Ok(new { d.Id, d.Name, d.BusinessCalendarId });
    }
}
