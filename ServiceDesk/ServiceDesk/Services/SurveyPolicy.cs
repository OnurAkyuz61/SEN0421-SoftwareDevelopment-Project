using ServiceDesk.Models;

namespace ServiceDesk.Services;

// appsettings.json → "Survey" bölümü.
//
// Anketin iki süresi var ve ikisi de kurumun kararı:
//   • ReminderAfterHours: çözümden ne kadar sonra hatırlatılacağı. Çok kısa
//     olursa kullanıcı talebi kapanmadan rahatsız edilir, çok uzun olursa
//     deneyimi hatırlamaz.
//   • ExpiresAfterDays: anketin doldurulabileceği süre (TTL). Aylar sonra
//     girilen puan hizmetin o günkü halini ölçmez, dönemsel ortalamayı
//     bozar (ISO 9001 md. 9.1.2: ölçüm izlenebilir ve anlamlı olmalı).
public class SurveySettings
{
    public const string Section = "Survey";

    // false ise hatırlatma job'ı hiç çalışmaz; anket yine doldurulabilir
    public bool ReminderEnabled { get; set; } = true;

    // Çözümden kaç saat sonra hatırlatma e-postası gider
    public int ReminderAfterHours { get; set; } = 24;

    // Anket çözümden kaç gün sonra kapanır. 0 = süre sınırı yok
    public int ExpiresAfterDays { get; set; } = 14;

    // Hatırlatma taramasının tekrar aralığı (dakika)
    public int CheckMinutes { get; set; } = 30;

    // Bir turda en fazla kaç talebe hatırlatma kuyruğa alınsın
    public int BatchSize { get; set; } = 200;
}

// Anket penceresinin kuralları: ne zaman kapanır, hatırlatma ne zaman gider.
//
// Pencere talep çözüldüğünde açılır (TicketWorkflow.Apply → SurveyInvitedAt).
// Hesabın dayanağı ResolvedAt değil SurveyInvitedAt: çözüldükten sonra
// kapatma, kapanışın tarihi, arşivleme gibi işlemler daveti ileri kaydırmaz.
// Bu geçişten önce çözülmüş talepler için (ve örnek veri üretici gibi
// doğrudan ResolvedAt yazan yollar için) ResolvedAt'e düşülür.
//
// Saf sınıf: birim testi yazılabiliyor (bkz. ServiceDesk.Tests).
public static class SurveyPolicy
{
    // Pencerenin başlangıcı
    public static DateTime? InvitedAt(Ticket ticket) => ticket.SurveyInvitedAt ?? ticket.ResolvedAt;

    // Anketin son doldurma anı. Süre sınırı kapalıysa (0) ya da talep henüz
    // çözülmediyse null.
    public static DateTime? Deadline(Ticket ticket, int expiresAfterDays)
    {
        if (expiresAfterDays <= 0) return null;
        return InvitedAt(ticket) is { } start ? start.AddDays(expiresAfterDays) : null;
    }

    // Anket süresi doldu mu? Henüz çözülmemiş talepte false: orada anket
    // "süresi dolmuş" değil, henüz açılmamıştır.
    public static bool IsExpired(Ticket ticket, DateTime nowUtc, int expiresAfterDays) =>
        Deadline(ticket, expiresAfterDays) is { } deadline && nowUtc > deadline;

    // Talep şu an doldurulabilir mi: çözülmüş, henüz puanlanmamış ve süresi
    // geçmemiş. (Yetki ve durum kontrolü çağıranda kalır.)
    public static bool CanSubmit(Ticket ticket, DateTime nowUtc, int expiresAfterDays) =>
        InvitedAt(ticket) != null
        && ticket.Survey == null
        && !IsExpired(ticket, nowUtc, expiresAfterDays);

    // Hatırlatma zamanı geldi mi (saf; SurveyReminderService bu kararı
    // SQL'de de uyguluyor, burada tek talep için doğrulanabilir).
    //
    // Koşullar: davet açık, daha önce hatırlatma gitmemiş, gecikme dolmuş ve
    // anket süresi henüz geçmemiş. Son koşul önemli: süresi dolmuş anket için
    // hatırlatma göndermek kullanıcıyı dolduramayacağı bir forma yollar.
    public static bool ReminderDue(Ticket ticket, DateTime nowUtc, int reminderAfterHours, int expiresAfterDays)
    {
        if (ticket.SurveyReminderSentAt != null) return false;
        if (InvitedAt(ticket) is not { } start) return false;
        if (nowUtc - start < TimeSpan.FromHours(Math.Max(0, reminderAfterHours))) return false;

        return !IsExpired(ticket, nowUtc, expiresAfterDays);
    }

    // Kullanıcıya gösterilecek kalan süre metni ("3 gün", "5 saat")
    public static string RemainingText(DateTime deadlineUtc, DateTime nowUtc)
    {
        var left = deadlineUtc - nowUtc;
        if (left <= TimeSpan.Zero) return "süre doldu";
        if (left.TotalHours < 1) return $"{Math.Max(1, (int)left.TotalMinutes)} dakika";
        if (left.TotalDays < 1) return $"{(int)left.TotalHours} saat";
        return $"{(int)left.TotalDays} gün";
    }
}
