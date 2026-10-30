using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Değişiklik (Change) onay zincirinin YETKİ kuralları.
//
// Kural daha önce ApprovalsController.Decide içinde dağınık duruyordu ve
// yalnızca "onayla" yolunu koruyordu: red işleminde hiçbir rol kontrolü yoktu,
// yani destek personeli tek başına bir değişikliği reddedip talebi
// kapatabiliyordu. Kurallar artık tek yerde:
//
//   • Onay akışı yalnızca Değişiklik taleplerinde işler.
//   • Son kullanıcı akışın tamamen dışındadır.
//   • Kimse kendi açtığı değişikliği karara bağlayamaz (görevlerin ayrılığı).
//   • Nihai onay ve red yöneticinin yetkisindedir; destek personeli yalnızca
//     yüksek riskli değişikliğin ilk (ara) onayını verebilir.
//   • Zinciri aynı kişi tek başına tamamlayamaz.
//
// Saf sınıf: veritabanına ve HTTP'ye bağımlı değil, birim testi yazılabiliyor.
public static class ApprovalPolicy
{
    public record Decision(bool Allowed, string? Error = null);

    // Şu an verilecek onay kaçıncı adım (verilmiş onaylar + 1)
    public static int NextStep(Ticket ticket) =>
        ticket.Approvals.Count(a => a.Decision == ApprovalStatus.Approved) + 1;

    // Bu adım zinciri tamamlıyor mu (nihai karar mı)
    public static bool IsFinalStep(Ticket ticket) =>
        NextStep(ticket) >= ticket.RequiredApprovalSteps;

    public static Decision CanDecide(Ticket ticket, AppUser user, bool approve)
    {
        if (ticket.Type != TicketType.Change)
            return new Decision(false, "Onay akışı yalnızca değişiklik taleplerinde işler.");

        if (ticket.ApprovalStatus != ApprovalStatus.Pending)
            return new Decision(false,
                $"{Ui.TicketNo(ticket)} zaten karara bağlanmış ({Ui.ApprovalText(ticket.ApprovalStatus)}).");

        if (user.Role == UserRole.User)
            return new Decision(false, "Değişiklik onayı yalnızca destek ve yönetici rollerinin yetkisindedir.");

        // Talebi açan kişi kendi değişikliğini onaylayamaz da reddedemez de
        if (ticket.RequesterId == user.Id)
            return new Decision(false, "Kendi açtığınız değişiklik talebini karara bağlayamazsınız.");

        // Red nihai karardır: talep kapanır ve bir daha açılamaz
        if (!approve && user.Role != UserRole.Admin)
            return new Decision(false, "Değişiklik talebini yalnızca yönetici reddedebilir.");

        if (approve && ticket.RequiredApprovalSteps > 1 &&
            ticket.Approvals.Any(a => a.ApproverId == user.Id))
            return new Decision(false, "Bu değişikliği zaten onayladınız; sonraki onay başka bir yetkiliden gelmeli.");

        if (approve && IsFinalStep(ticket) && user.Role != UserRole.Admin)
            return new Decision(false, "Değişikliğin nihai onayını yalnızca yönetici verebilir.");

        return new Decision(true);
    }
}
