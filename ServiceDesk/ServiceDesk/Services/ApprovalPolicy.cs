using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Onay akışının ayarları (appsettings.json → "Approvals")
public class ApprovalSettings
{
    public const string Section = "Approvals";

    // Planlanan uygulama penceresi girilmeden onay verilemesin mi
    public bool RequirePlannedWindow { get; set; } = false;

    // Planlanan pencerenin bitişi geçmişse onay engellensin mi
    public bool BlockOutsidePlannedWindow { get; set; } = true;

    // Yüksek riskli değişikliğin nihai onayı CAB toplantı kaydı ister mi
    public bool RequireCabForHighRisk { get; set; } = true;

    // Onay bekleyen talep için ilk hatırlatma ve sonraki hatırlatmaların aralığı (saat)
    public int ReminderAfterHours { get; set; } = 24;
    public int ReminderIntervalHours { get; set; } = 24;
    public int ReminderCheckMinutes { get; set; } = 30;

    // Reddedilen talep en fazla kaç kez düzeltilip yeniden gönderilebilir
    public int MaxResubmissions { get; set; } = 3;

    // Vekaletin en uzun süresi (gün)
    public int MaxDelegationDays { get; set; } = 60;
}

// Kararı veren kişinin bağlamı: zaman, aktif vekaletler, ayarlar.
// Vekaleti veren yöneticiler (Delegators) listesi ApprovalAuthority
// tarafından veritabanından doldurulur; politika saf kalır.
public sealed record ApprovalContext(DateTime NowUtc, IReadOnlyList<AppUser> Delegators, ApprovalSettings Settings)
{
    // Eski çağrılar için: vekalet yok, pencere ve CAB kuralları kapalı
    public static ApprovalContext Default => new(AppTime.UtcNow, Array.Empty<AppUser>(),
        new ApprovalSettings { BlockOutsidePlannedWindow = false, RequireCabForHighRisk = false });
}

// Değişiklik (Change) onay zincirinin YETKİ kuralları.
//
//   • Onay akışı yalnızca Değişiklik taleplerinde işler.
//   • Son kullanıcı akışın tamamen dışındadır.
//   • Kimse kendi açtığı değişikliği karara bağlayamaz (görevlerin ayrılığı).
//   • Nihai onay ve red yöneticinin yetkisindedir; destek personeli yalnızca
//     ara adımları onaylayabilir — yöneticiden VEKALET almadıkça.
//   • Zinciri aynı kişi tek başına tamamlayamaz (vekil, vekaleti verenin
//     yerine de olsa aynı turda ikinci kez onay veremez).
//   • Planlanan uygulama penceresi geçmişse onay verilemez.
//   • Yüksek riskli değişikliğin nihai onayı CAB toplantı kaydı ister.
//
// Saf sınıf: veritabanına ve HTTP'ye bağımlı değil, birim testi yazılabiliyor.
public static class ApprovalPolicy
{
    public record Decision(bool Allowed, string? Error = null, AppUser? OnBehalfOf = null);

    // Şu an verilecek onay kaçıncı adım (geçerli turdaki onaylar + 1)
    public static int NextStep(Ticket ticket) =>
        ticket.CurrentRoundApprovals.Count(a => a.Decision == ApprovalStatus.Approved) + 1;

    // Bu adım zinciri tamamlıyor mu (nihai karar mı)
    public static bool IsFinalStep(Ticket ticket) =>
        NextStep(ticket) >= ticket.RequiredApprovalSteps;

    public static Decision CanDecide(Ticket ticket, AppUser user, bool approve) =>
        CanDecide(ticket, user, approve, ApprovalContext.Default);

    public static Decision CanDecide(Ticket ticket, AppUser user, bool approve, ApprovalContext ctx)
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

        var round = ticket.CurrentRoundApprovals.ToList();
        var final = IsFinalStep(ticket);

        if (approve && ticket.RequiredApprovalSteps > 1 &&
            round.Any(a => a.ApproverId == user.Id || a.OnBehalfOfId == user.Id))
            return new Decision(false, "Bu değişikliği zaten onayladınız; sonraki onay başka bir yetkiliden gelmeli.");

        // Yönetici yetkisi gerekiyor mu (red her zaman; onayda nihai adım)
        var needsAdmin = !approve || final;
        AppUser? onBehalfOf = null;

        if (needsAdmin && user.Role != UserRole.Admin)
        {
            // Vekalet: yöneticiden yetki devralmış personel onun yerine karar verir.
            // Vekaleti veren talep sahibi olamaz ve bu turda zaten onay vermiş olamaz.
            onBehalfOf = ctx.Delegators.FirstOrDefault(d =>
                d.Role == UserRole.Admin && d.IsActive && d.Id != ticket.RequesterId
                && !round.Any(a => a.ApproverId == d.Id || a.OnBehalfOfId == d.Id));

            if (onBehalfOf == null)
                return new Decision(false, approve
                    ? "Değişikliğin nihai onayını yalnızca yönetici (veya vekili) verebilir."
                    : "Değişiklik talebini yalnızca yönetici (veya vekili) reddedebilir.");
        }

        if (approve)
        {
            var d = ticket.ChangeDetail;

            if (ctx.Settings.RequirePlannedWindow && (d?.PlannedStart == null || d.PlannedEnd == null))
                return new Decision(false, "Onay için önce planlanan uygulama penceresi (başlangıç ve bitiş) girilmeli.");

            if (ctx.Settings.BlockOutsidePlannedWindow && d?.PlannedEnd is { } end && ctx.NowUtc > end)
                return new Decision(false,
                    "Planlanan uygulama penceresi geçti; onay verilemez. Planı yeni bir tarihle güncelleyin.");

            if (final && ctx.Settings.RequireCabForHighRisk && d?.Risk == RiskLevel.High
                && string.IsNullOrWhiteSpace(d.CabMeetingUrl))
                return new Decision(false, "Yüksek riskli değişikliğin nihai onayı için CAB toplantı kaydı bağlantısı girilmeli.");
        }

        return new Decision(true, null, onBehalfOf);
    }

    // Reddedilen talep düzeltilip yeniden onaya gönderilebilir mi
    public static Decision CanResubmit(Ticket ticket, AppUser user, int maxResubmissions)
    {
        if (ticket.Type != TicketType.Change)
            return new Decision(false, "Yalnızca değişiklik talepleri yeniden onaya gönderilebilir.");

        if (ticket.ApprovalStatus != ApprovalStatus.Rejected)
            return new Decision(false, "Yalnızca reddedilmiş değişiklik talebi yeniden gönderilebilir.");

        // Talep sahibi ya da destek / yönetici (talep adına düzeltme yapabilir)
        if (ticket.RequesterId != user.Id && user.Role == UserRole.User)
            return new Decision(false, "Bu talebi yeniden gönderme yetkiniz yok.");

        if (ticket.ApprovalRound - 1 >= maxResubmissions)
            return new Decision(false, $"Bu talep en fazla {maxResubmissions} kez yeniden gönderilebilir; yeni bir talep açın.");

        return new Decision(true);
    }
}

// Onay sonrası uygulama / geri alma durum geçişleri (saf)
public static class ChangeImplementationRules
{
    public record Result(bool Allowed, string? Error = null);

    public static Result CanMove(Ticket ticket, ImplementationStatus target, DateTime nowUtc)
    {
        if (ticket.Type != TicketType.Change || ticket.ChangeDetail == null)
            return new Result(false, "Uygulama durumu yalnızca değişiklik taleplerinde tutulur.");

        if (ticket.ApprovalStatus != ApprovalStatus.Approved)
            return new Result(false, "Onaylanmamış değişiklik uygulanamaz.");

        var from = ticket.ChangeDetail.ImplementationStatus;
        if (from == target) return new Result(false, "Durum zaten bu.");

        var ok = (from, target) switch
        {
            (ImplementationStatus.NotStarted, ImplementationStatus.InProgress) => true,
            (ImplementationStatus.InProgress, ImplementationStatus.Implemented) => true,
            (ImplementationStatus.InProgress, ImplementationStatus.Failed) => true,
            (ImplementationStatus.Failed, ImplementationStatus.RolledBack) => true,
            (ImplementationStatus.Implemented, ImplementationStatus.RolledBack) => true,
            (ImplementationStatus.Failed, ImplementationStatus.InProgress) => true,   // yeniden deneme
            _ => false
        };
        if (!ok)
            return new Result(false, $"{Ui.ImplementationText(from)} → {Ui.ImplementationText(target)} geçişine izin verilmez.");

        // Uygulamaya planlanan pencere içinde başlanır
        var d = ticket.ChangeDetail;
        if (target == ImplementationStatus.InProgress && d.PlannedStart is { } start && d.PlannedEnd is { } end
            && (nowUtc < start || nowUtc > end))
            return new Result(false, "Uygulama yalnızca planlanan pencere içinde başlatılabilir.");

        return new Result(true);
    }

    public static void Apply(ChangeDetail d, ImplementationStatus target, DateTime nowUtc, string? note)
    {
        d.ImplementationStatus = target;
        switch (target)
        {
            case ImplementationStatus.InProgress: d.ImplementationStartedAt = nowUtc; break;
            case ImplementationStatus.Implemented: d.ImplementedAt = nowUtc; break;
            case ImplementationStatus.RolledBack: d.RolledBackAt = nowUtc; break;
        }
        if (!string.IsNullOrWhiteSpace(note)) d.ImplementationNote = note.Trim();
    }
}

// Onay zinciri kurallarının çözümü (saf)
public static class ApprovalChainPolicy
{
    public static bool Matches(ApprovalChainRule r, Ticket t) =>
        r.IsActive
        && (r.CategoryId == null || r.CategoryId == t.CategoryId)
        && (r.MinPriority == null || t.Priority >= r.MinPriority)
        && (r.Risk == null || (t.ChangeDetail?.Risk ?? RiskLevel.Low) == r.Risk);

    // Uyan kurallar içinde en çok adım isteyen; hiçbiri uymazsa null (risk varsayılanı)
    public static int? Resolve(IEnumerable<ApprovalChainRule> rules, Ticket t)
    {
        var steps = rules.Where(r => Matches(r, t)).Select(r => Math.Clamp(r.RequiredSteps, 1, 5)).ToList();
        if (steps.Count == 0) return null;
        // Kural risk varsayılanından azını isteyemez (yüksek risk en az 2)
        return Math.Max(steps.Max(), t.DefaultApprovalSteps);
    }
}
