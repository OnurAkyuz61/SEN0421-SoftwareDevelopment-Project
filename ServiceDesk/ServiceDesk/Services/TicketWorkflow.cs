using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Talebin durum geçiş kuralları. Controller'ın içinde dağınık durduğu sürece
// talep başka bir yerden (API, toplu içe aktarma, arka plan işi) güncellenirse
// kural uygulanmıyordu. Tek giriş noktası burası.
//
// Saf bir sınıf: veritabanına ve HTTP'ye bağımlı değil, bu yüzden birim testi
// yazılabiliyor (bkz. ServiceDesk.Tests).
public static class TicketWorkflow
{
    public record TransitionResult(bool Allowed, string? Error = null, bool Reopened = false);

    public static bool IsOpen(TicketStatus s) =>
        s is TicketStatus.New or TicketStatus.InProgress or TicketStatus.WaitingForCustomer;
    public static bool IsFinished(TicketStatus s) => s is TicketStatus.Resolved or TicketStatus.Closed;

    // Geçişin kurallara uyup uymadığı. Talebi DEĞİŞTİRMEZ, yalnızca karar verir.
    public static TransitionResult CanTransition(Ticket ticket, TicketStatus target)
    {
        if (ticket.Status == target)
            return new TransitionResult(true);

        // Reddedilmiş talep yeniden açılamaz.
        // Daha özel kural olduğu için onay kontrolünden ÖNCE bakılır; aksi halde
        // kullanıcı "onaylanmadan işleme alınamaz" gibi yanıltıcı bir mesaj alır.
        if (ticket.ApprovalStatus == ApprovalStatus.Rejected && IsOpen(target))
            return new TransitionResult(false, "Reddedilmiş bir değişiklik talebi yeniden açılamaz.");

        // Onaylanmamış değişiklik talebi işleme alınamaz (Change Management kuralı)
        if (ticket.Type == TicketType.Change
            && ticket.ApprovalStatus != ApprovalStatus.Approved
            && target is TicketStatus.InProgress or TicketStatus.Resolved or TicketStatus.WaitingForCustomer)
        {
            return new TransitionResult(false, "Bu değişiklik talebi onaylanmadan işleme alınamaz.");
        }

        // "Müşteri bekleniyor" yalnızca açık talep için kullanılır
        if (target == TicketStatus.WaitingForCustomer && IsFinished(ticket.Status))
            return new TransitionResult(false, "Çözülmüş veya kapanmış talep müşteri beklemeye alınamaz.");

        var reopened = IsFinished(ticket.Status) && IsOpen(target);
        return new TransitionResult(true, null, reopened);
    }

    // Geçişi uygular: durum, tarihler ve yeniden açılma sayacı tek yerden yönetilir.
    // Çağırmadan önce CanTransition ile izin alınmalıdır.
    public static void Apply(Ticket ticket, TicketStatus target, DateTime nowUtc)
    {
        var wasFinished = IsFinished(ticket.Status);

        // SLA duraklaması: beklemeye girerken başlar. Çıkışta asıl hesap
        // TicketSlaService.ApplyStatusAsync'te mesai takvimiyle yapılır; buraya
        // duraklama açık gelirse (servis atlanmışsa) takvim saatiyle kapatılır.
        if (target == TicketStatus.WaitingForCustomer)
            ticket.SlaPausedAt ??= nowUtc;
        else if (ticket.SlaPausedAt is { } pausedAt)
        {
            var paused = nowUtc - pausedAt;
            ticket.SlaPausedAt = null;
            ticket.SlaPausedMinutes += (int)paused.TotalMinutes;
            ticket.SlaDueAt += paused;
            if (ticket.FirstRespondedAt == null && ticket.FirstResponseDueAt != null)
                ticket.FirstResponseDueAt = ticket.FirstResponseDueAt.Value + paused;
        }

        ticket.Status = target;

        if (IsFinished(target))
        {
            ticket.ResolvedAt ??= nowUtc;
            if (target == TicketStatus.Closed) ticket.ClosedAt ??= nowUtc;

            // Anket penceresi çözümle birlikte açılır: hatırlatma gecikmesi ve
            // doldurma süresi (SurveyPolicy) bu andan sayılır. Çözüldükten
            // sonra kapatma ikinci bir davet açmaz (??=).
            ticket.SurveyInvitedAt ??= nowUtc;
        }
        else if (wasFinished)
        {
            // Yeniden açılış: sayaç artar, önceki çözüm bilgisi geçmişte kalır
            ticket.ReopenCount++;
            ticket.ResolvedAt = null;
            ticket.ClosedAt = null;
            // Önceki çözüm geçersiz: bir sonraki çözümde kod yeniden seçilir
            ticket.ResolutionCode = null;
            ticket.RootCause = null;

            // Anket penceresi de kapanır: iş henüz bitmedi, sorulacak bir şey
            // yok. Bir sonraki çözümde pencere sıfırdan açılır ve hatırlatma
            // yeniden gönderilebilir.
            ticket.SurveyInvitedAt = null;
            ticket.SurveyReminderSentAt = null;
        }
    }

    // Onay süreci başladı mı: geçerli turda en az bir karar verilmiş ya da talep
    // karara bağlanmış. Yeniden gönderilen talepte önceki turun kararları sayılmaz.
    public static bool IsApprovalStarted(Ticket ticket) =>
        ticket.CurrentRoundApprovals.Any()
        || ticket.ApprovalStatus is ApprovalStatus.Approved or ApprovalStatus.Rejected;

    // İlk yanıt yalnızca bir kez işaretlenir ve iç notlar saymaz
    // Geç verilen ilk yanıt, SLA taraması henüz yakalamamış olsa da ihlal sayılır.
    public static bool MarkFirstResponse(Ticket ticket, bool isInternalNote, DateTime nowUtc)
    {
        if (isInternalNote || ticket.FirstRespondedAt != null) return false;
        ticket.FirstRespondedAt = nowUtc;
        if (ticket.FirstResponseDueAt != null && nowUtc > ticket.FirstResponseDueAt && !ticket.IsFirstResponseBreached)
        {
            ticket.IsFirstResponseBreached = true;
            ticket.FirstResponseBreachedAt = nowUtc;
        }
        return true;
    }

    // Kapanış (Çözüldü / Kapandı) için çözüm kodu ve kök neden zorunlu.
    // Önceden girilmişse (ör. çözüldükten sonra kapatma) tekrar istenmez.
    public static string? ValidateResolution(Ticket ticket, TicketStatus target, ResolutionCode? code, string? rootCause)
    {
        if (!IsFinished(target) || ticket.ResolutionCode != null) return null;
        if (code == null) return "Talebi kapatmak için çözüm kodu seçin.";
        if (string.IsNullOrWhiteSpace(rootCause)) return "Talebi kapatmak için kök neden / çözüm açıklaması yazın.";
        if (rootCause.Trim().Length > 1000) return "Kök neden en fazla 1000 karakter olabilir.";
        return null;
    }
}
