using ServiceDesk.Models;

namespace ServiceDesk.Services;

// SLA hedef tarihlerini hesaplar.
//
// Takvim saatiyle değil MESAİ saatiyle sayar: Cuma 17:00'de açılan 8 saatlik
// bir talebin hedefi Cumartesi 01:00 değil, Pazartesi 16:00'dır. Hafta sonları
// ve takvimdeki resmî tatiller atlanır.
//
// Tüm giriş ve çıkışlar UTC'dir; mesai saatleri yerel takvime göre yorumlanır.
public static class SlaCalculator
{
    // Sonsuz döngüye karşı emniyet: 10 yıldan uzak bir hedef üretilmez
    private const int MaxDaysScanned = 3650;

    public static DateTime AddWorkingMinutes(
        DateTime startUtc, int minutes, BusinessCalendar? calendar, IReadOnlySet<DateTime>? holidays = null)
    {
        if (minutes <= 0) return startUtc;

        // Takvim tanımlı değilse 7x24 çalışır
        if (calendar == null) return startUtc.AddMinutes(minutes);

        var workDays = ParseWorkDays(calendar.WorkDays);
        var dayLength = calendar.WorkEndMinutes - calendar.WorkStartMinutes;

        // Bozuk takvim yapılandırması sessizce yanlış hedef üretmesin
        if (dayLength <= 0 || workDays.Count == 0) return startUtc.AddMinutes(minutes);

        var local = AppTime.ToLocal(startUtc);
        var remaining = minutes;

        for (var guard = 0; guard < MaxDaysScanned; guard++)
        {
            if (!IsWorkingDay(local, workDays, holidays))
            {
                local = NextDayStart(local, calendar);
                continue;
            }

            var minuteOfDay = local.Hour * 60 + local.Minute;

            // Mesai başlamadıysa başlangıca çek, bittiyse ertesi güne geç
            if (minuteOfDay < calendar.WorkStartMinutes)
            {
                local = local.Date.AddMinutes(calendar.WorkStartMinutes);
                minuteOfDay = calendar.WorkStartMinutes;
            }
            else if (minuteOfDay >= calendar.WorkEndMinutes)
            {
                local = NextDayStart(local, calendar);
                continue;
            }

            var availableToday = calendar.WorkEndMinutes - minuteOfDay;
            if (remaining <= availableToday)
                return AppTime.ToUtc(local.AddMinutes(remaining));

            remaining -= availableToday;
            local = NextDayStart(local, calendar);
        }

        // Buraya düşmek yapılandırma hatası demektir; takvim saatine düşülür
        return startUtc.AddMinutes(minutes);
    }

    private static DateTime NextDayStart(DateTime local, BusinessCalendar calendar) =>
        local.Date.AddDays(1).AddMinutes(calendar.WorkStartMinutes);

    private static bool IsWorkingDay(DateTime local, HashSet<int> workDays, IReadOnlySet<DateTime>? holidays)
    {
        // Pazar = 7 (ISO)
        var iso = local.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)local.DayOfWeek;
        if (!workDays.Contains(iso)) return false;
        return holidays == null || !holidays.Contains(local.Date);
    }

    private static HashSet<int> ParseWorkDays(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
             .Select(s => int.TryParse(s, out var d) ? d : 0)
             .Where(d => d is >= 1 and <= 7)
             .ToHashSet();
}
