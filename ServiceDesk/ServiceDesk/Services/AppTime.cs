namespace ServiceDesk.Services;

// Uygulamanın tek zaman kaynağı.
// Veritabanında bütün tarihler UTC saklanır; ekranda yerel saate çevrilir.
// Böylece sunucu saat dilimi değişse veya yaz saati uygulansa bile
// SLA hesapları kaymaz ve farklı kayıtlar aynı eksende karşılaştırılabilir.
public static class AppTime
{
    public static DateTime UtcNow => DateTime.UtcNow;

    private static readonly TimeZoneInfo DisplayZone = ResolveZone();

    private static TimeZoneInfo ResolveZone()
    {
        // Windows ve Linux farklı kimlikler kullanır; ikisi de bulunamazsa sunucunun kendi saati
        foreach (var id in new[] { "Turkey Standard Time", "Europe/Istanbul" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Local;
    }

    // Veritabanından okunan değerin Kind'ı Unspecified gelir; UTC olarak işaretleyip çeviriyoruz
    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), DisplayZone);

    public static DateTime? ToLocal(DateTime? utc) => utc == null ? null : ToLocal(utc.Value);

    // Yerel saatten UTC'ye: SLA hesabı mesai saatlerini yerel takvimde yürütür,
    // sonucu veritabanına yazmadan önce buradan geçirir
    public static DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), DisplayZone);

    // Grafiklerde "bugün" yerel takvime göre belirlenir
    public static DateTime LocalToday => ToLocal(DateTime.UtcNow).Date;
}
