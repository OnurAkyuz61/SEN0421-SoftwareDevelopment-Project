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

    public static bool IsOpen(TicketStatus s) => s is TicketStatus.New or TicketStatus.InProgress;
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
            && target is TicketStatus.InProgress or TicketStatus.Resolved)
        {
            return new TransitionResult(false, "Bu değişiklik talebi onaylanmadan işleme alınamaz.");
        }

        var reopened = IsFinished(ticket.Status) && IsOpen(target);
        return new TransitionResult(true, null, reopened);
    }

    // Geçişi uygular: durum, tarihler ve yeniden açılma sayacı tek yerden yönetilir.
    // Çağırmadan önce CanTransition ile izin alınmalıdır.
    public static void Apply(Ticket ticket, TicketStatus target, DateTime nowUtc)
    {
        var wasFinished = IsFinished(ticket.Status);

        ticket.Status = target;

        if (IsFinished(target))
        {
            ticket.ResolvedAt ??= nowUtc;
            if (target == TicketStatus.Closed) ticket.ClosedAt ??= nowUtc;
        }
        else if (wasFinished)
        {
            // Yeniden açılış: sayaç artar, önceki çözüm bilgisi geçmişte kalır
            ticket.ReopenCount++;
            ticket.ResolvedAt = null;
            ticket.ClosedAt = null;
        }
    }

    // Onay süreci başladı mı: geçerli turda en az bir karar verilmiş ya da talep
    // karara bağlanmış. Yeniden gönderilen talepte önceki turun kararları sayılmaz.
    public static bool IsApprovalStarted(Ticket ticket) =>
        ticket.CurrentRoundApprovals.Any()
        || ticket.ApprovalStatus is ApprovalStatus.Approved or ApprovalStatus.Rejected;

    // İlk yanıt yalnızca bir kez işaretlenir ve iç notlar saymaz
    public static bool MarkFirstResponse(Ticket ticket, bool isInternalNote, DateTime nowUtc)
    {
        if (isInternalNote || ticket.FirstRespondedAt != null) return false;
        ticket.FirstRespondedAt = nowUtc;
        return true;
    }
}
