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
        TicketStatus.WaitingForCustomer => "Müşteri bekleniyor",
        TicketStatus.Resolved => "Çözüldü", _ => "Kapandı"
    };

    public static string StatusTag(TicketStatus s) => s switch
    {
        TicketStatus.New => "tag-blue", TicketStatus.InProgress => "tag-amber",
        TicketStatus.WaitingForCustomer => "tag-gray",
        TicketStatus.Resolved => "tag-green", _ => "tag-gray"
    };

    public static string ResolutionText(ResolutionCode? c) => c switch
    {
        null => "-",
        ResolutionCode.Fixed => "Kalıcı çözüm",
        ResolutionCode.Workaround => "Geçici çözüm",
        ResolutionCode.Information => "Bilgi verildi",
        ResolutionCode.UserError => "Kullanıcı hatası",
        ResolutionCode.CannotReproduce => "Tekrarlanamadı",
        ResolutionCode.ChangeImplemented => "Değişiklik uygulandı",
        ResolutionCode.Duplicate => "Mükerrer (birleştirildi)",
        _ => "İptal edildi"
    };

    public static string TypeText(TicketType t) => t == TicketType.Incident ? "Arıza" : "Değişiklik";

    // Kategori artık enum değil tablo; ad doğrudan kayıttan gelir
    public static string CategoryText(Category? c) => c?.Name ?? "-";

    public static string NonconformityStatusText(NonconformityStatus s) => s switch
    {
        NonconformityStatus.Closed => "Kapalı", NonconformityStatus.InProgress => "İşlemde", _ => "Açık"
    };

    // Renk eşlemesi Nonconformities/Index görünümünün içinde satır arasında
    // duruyordu; etiket rengi de metin gibi tek yerden okunsun diye buraya alındı
    public static string NonconformityStatusTag(NonconformityStatus s) => s switch
    {
        NonconformityStatus.Closed => "tag-green", NonconformityStatus.InProgress => "tag-amber", _ => "tag-red"
    };

    public static string NonconformitySourceText(NonconformitySource s) => s switch
    {
        NonconformitySource.SlaBreach => "SLA ihlali",
        NonconformitySource.LowSatisfaction => "Düşük memnuniyet",
        NonconformitySource.Audit => "İç denetim",
        _ => "Elle açıldı"
    };

    public static string KnowledgeStatusText(KnowledgeArticleStatus s) => s switch
    {
        KnowledgeArticleStatus.Draft => "Taslak",
        KnowledgeArticleStatus.PendingApproval => "Onay bekliyor",
        KnowledgeArticleStatus.Published => "Yayında",
        _ => "Reddedildi"
    };

    public static string KnowledgeStatusTag(KnowledgeArticleStatus s) => s switch
    {
        KnowledgeArticleStatus.Draft => "tag-gray",
        KnowledgeArticleStatus.PendingApproval => "tag-amber",
        KnowledgeArticleStatus.Published => "tag-green",
        _ => "tag-red"
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
        LoginResult.Inactive => "Hesap pasif",
        _ => "Çok fazla deneme"
    };

    public static string LoginResultTag(LoginResult r) => r switch
    {
        LoginResult.Success => "tag-green",
        LoginResult.DevLogin => "tag-blue",
        LoginResult.NoPhone or LoginResult.OtpExpired or LoginResult.Inactive => "tag-amber",
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

    public static string ScanStatusText(AttachmentScanStatus s) => s switch
    {
        AttachmentScanStatus.Clean => "Temiz",
        AttachmentScanStatus.Suspicious => "Şüpheli",
        AttachmentScanStatus.Failed => "Taranamadı",
        AttachmentScanStatus.Skipped => "Tarama kapsamı dışı",
        _ => "Taranıyor"
    };

    public static string ScanStatusTag(AttachmentScanStatus s) => s switch
    {
        AttachmentScanStatus.Clean or AttachmentScanStatus.Skipped => "tag-green",
        AttachmentScanStatus.Suspicious => "tag-red",
        AttachmentScanStatus.Failed => "tag-amber",
        _ => "tag-gray"
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
        AuditAction.AttachmentPurged => "Ek dosya kalıcı silindi",
        AuditAction.AttachmentOrphanDeleted => "Sahipsiz dosya silindi",
        AuditAction.ForceLogout => "Zorla çıkış",
        _ => a.ToString()
    };
}
