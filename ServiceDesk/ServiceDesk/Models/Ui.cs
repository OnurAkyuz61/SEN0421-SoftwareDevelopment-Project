namespace ServiceDesk.Models;

// Arayüzde etiket renkleri (site.css .tag-*) ve Türkçe metinler
public static class Ui
{
    public static string PriorityText(Priority p) => p switch
    {
        Priority.Critical => "Kritik", Priority.High => "Yüksek", Priority.Medium => "Orta", _ => "Düşük"
    };

    public static string PriorityTag(Priority p) => p switch
    {
        Priority.Critical => "tag-red", Priority.High => "tag-amber", Priority.Medium => "tag-blue", _ => "tag-gray"
    };

    public static string StatusText(TicketStatus s) => s switch
    {
        TicketStatus.New => "Yeni", TicketStatus.InProgress => "İşlemde",
        TicketStatus.Resolved => "Çözüldü", _ => "Kapandı"
    };

    public static string StatusTag(TicketStatus s) => s switch
    {
        TicketStatus.New => "tag-blue", TicketStatus.InProgress => "tag-amber",
        TicketStatus.Resolved => "tag-green", _ => "tag-gray"
    };

    public static string TypeText(TicketType t) => t == TicketType.Incident ? "Arıza" : "Değişiklik";

    // Kategori artık enum değil tablo; ad doğrudan kayıttan gelir
    public static string CategoryText(Category? c) => c?.Name ?? "-";

    public static string NonconformityStatusText(NonconformityStatus s) => s switch
    {
        NonconformityStatus.Closed => "Kapalı", NonconformityStatus.InProgress => "İşlemde", _ => "Açık"
    };

    public static string NonconformitySourceText(NonconformitySource s) => s switch
    {
        NonconformitySource.SlaBreach => "SLA ihlali",
        NonconformitySource.LowSatisfaction => "Düşük memnuniyet",
        NonconformitySource.Audit => "İç denetim",
        _ => "Elle açıldı"
    };

    public static string RiskText(RiskLevel r) => r switch
    {
        RiskLevel.High => "Yüksek", RiskLevel.Medium => "Orta", _ => "Düşük"
    };

    public static string LoginResultText(LoginResult r) => r switch
    {
        LoginResult.Success => "Başarılı",
        LoginResult.WrongPassword => "Şifre hatalı",
        LoginResult.UnknownEmail => "Hesap yok",
        LoginResult.NoPhone => "Telefon tanımsız",
        LoginResult.OtpFailed => "OTP hatalı",
        LoginResult.OtpExpired => "OTP süresi doldu",
        LoginResult.DevLogin => "Geliştirme girişi",
        _ => "Çok fazla deneme"
    };

    public static string LoginResultTag(LoginResult r) => r switch
    {
        LoginResult.Success => "tag-green",
        LoginResult.DevLogin => "tag-blue",
        LoginResult.NoPhone or LoginResult.OtpExpired => "tag-amber",
        _ => "tag-red"
    };

    public static string RoleText(UserRole r) => r switch
    {
        UserRole.Admin => "Yönetici", UserRole.Agent => "Destek", _ => "Kullanıcı"
    };

    // INC-2026-00042 / CHG-2026-00007. Id benzersiz olduğu için numara da benzersizdir.
    public static string BuildTicketNumber(Ticket t) =>
        $"{(t.Type == TicketType.Change ? "CHG" : "INC")}-{Services.AppTime.ToLocal(t.CreatedAt):yyyy}-{t.Id:D5}";

    // Numarası henüz üretilmemiş eski kayıtlar için geri düşüş
    public static string TicketNo(Ticket t) => t.Number ?? $"#{t.Id}";

    public static string ApprovalText(ApprovalStatus a) => a switch
    {
        ApprovalStatus.Pending => "Onay bekliyor", ApprovalStatus.Approved => "Onaylandı",
        ApprovalStatus.Rejected => "Reddedildi", _ => "-"
    };

    public static string ApprovalTag(ApprovalStatus a) => a switch
    {
        ApprovalStatus.Pending => "tag-amber", ApprovalStatus.Approved => "tag-green",
        ApprovalStatus.Rejected => "tag-red", _ => "tag-gray"
    };

    public static string FileSize(long bytes) =>
        bytes < 1024 * 1024 ? $"{Math.Max(1, bytes / 1024.0):0.#} KB" : $"{bytes / 1024.0 / 1024.0:0.#} MB";

    public static string ImplementationText(ImplementationStatus s) => s switch
    {
        ImplementationStatus.InProgress => "Uygulanıyor",
        ImplementationStatus.Implemented => "Uygulandı",
        ImplementationStatus.Failed => "Başarısız",
        ImplementationStatus.RolledBack => "Geri alındı",
        _ => "Başlamadı"
    };

    public static string ImplementationTag(ImplementationStatus s) => s switch
    {
        ImplementationStatus.InProgress => "tag-blue",
        ImplementationStatus.Implemented => "tag-green",
        ImplementationStatus.Failed => "tag-red",
        ImplementationStatus.RolledBack => "tag-amber",
        _ => "tag-gray"
    };

    public static string EscalationTargetText(EscalationTarget t) =>
        t == EscalationTarget.TeamLead ? "Ekip lideri" : "Rol";

    public static string OtpStatusText(OtpDeliveryStatus s) => s switch
    {
        OtpDeliveryStatus.Queued => "Kuyrukta",
        OtpDeliveryStatus.Sent => "Gönderildi",
        OtpDeliveryStatus.Delivered => "Teslim edildi",
        OtpDeliveryStatus.Read => "Okundu",
        OtpDeliveryStatus.Disabled => "Kanal kapalı",
        _ => "Başarısız"
    };

    public static string OtpStatusTag(OtpDeliveryStatus s) => s switch
    {
        OtpDeliveryStatus.Delivered or OtpDeliveryStatus.Read => "tag-green",
        OtpDeliveryStatus.Sent or OtpDeliveryStatus.Queued => "tag-blue",
        OtpDeliveryStatus.Disabled => "tag-gray",
        _ => "tag-red"
    };

    public static string AuditActionText(AuditAction a) => a switch
    {
        AuditAction.OtpFailed => "OTP hatalı kod",
        AuditAction.OtpExpired => "OTP süresi doldu",
        AuditAction.OtpAttemptsExceeded => "OTP deneme sınırı",
        AuditAction.OtpSendLimitExceeded => "OTP gönderim sınırı",
        AuditAction.OtpDeliveryFailed => "OTP gönderilemedi",
        AuditAction.AccountLocked => "Hesap kilitlendi",
        AuditAction.ManualEscalation => "Manuel eskalasyon",
        AuditAction.DelegationCreated => "Vekalet verildi",
        AuditAction.DelegationRevoked => "Vekalet kaldırıldı",
        AuditAction.ChangeResubmitted => "Yeniden onaya gönderildi",
        AuditAction.ChangeImplementationUpdated => "Uygulama durumu",
        AuditAction.RateLimited => "İstek sınırı aşıldı",
        _ => a.ToString()
    };
}
