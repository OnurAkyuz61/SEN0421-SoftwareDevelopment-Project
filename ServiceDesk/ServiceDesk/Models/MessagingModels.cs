using System.ComponentModel.DataAnnotations;
using ServiceDesk.Services;

namespace ServiceDesk.Models;

// ── OTP gönderimi ve denetim ─────────────────────────────────────────────

// Kanala göre teslim durumu. SMS sağlayıcıları genelde Sent → Delivered
// geri bildirimi verir; WhatsApp ek olarak Read bildirir.
public enum OtpDeliveryStatus
{
    [Display(Name = "Kuyrukta")] Queued = 0,
    [Display(Name = "Gönderildi")] Sent = 1,
    [Display(Name = "Teslim edildi")] Delivered = 2,
    [Display(Name = "Okundu")] Read = 3,
    [Display(Name = "Başarısız")] Failed = 4,
    [Display(Name = "Kanal kapalı")] Disabled = 5
}

// Her OTP gönderim denemesinin kaydı: kime, hangi kanaldan, sağlayıcının
// verdiği mesaj numarası ve son teslim durumu. Kodun kendisi saklanmaz.
public class OtpDeliveryLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }
    public AppUser? User { get; set; }

    public OtpDeliveryChannel Channel { get; set; }

    // Telefonun maskelenmiş hali (0532 *** ** 67); tam numara loga yazılmaz
    [Required, StringLength(30)]
    public string MaskedPhone { get; set; } = "";

    public OtpDeliveryStatus Status { get; set; } = OtpDeliveryStatus.Queued;

    // Sağlayıcı adı ve sağlayıcının mesaja verdiği numara (durum sorgusu için)
    [StringLength(50)]
    public string? Provider { get; set; }

    [StringLength(100)]
    public string? ProviderMessageId { get; set; }

    [StringLength(300)]
    public string? Error { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    // Bu giriş denemesinde kaçıncı gönderim
    public int SendNumber { get; set; }

    public DateTime CreatedAt { get; set; } = AppTime.UtcNow;
    public DateTime? StatusUpdatedAt { get; set; }
}

public enum AuditAction
{
    [Display(Name = "OTP hatalı kod")] OtpFailed = 0,
    [Display(Name = "OTP süresi doldu")] OtpExpired = 1,
    [Display(Name = "OTP deneme sınırı")] OtpAttemptsExceeded = 2,
    [Display(Name = "OTP gönderim sınırı")] OtpSendLimitExceeded = 3,
    [Display(Name = "OTP gönderilemedi")] OtpDeliveryFailed = 4,
    [Display(Name = "Hesap kilitlendi")] AccountLocked = 5,
    [Display(Name = "Manuel eskalasyon")] ManualEscalation = 10,
    [Display(Name = "Vekalet verildi")] DelegationCreated = 20,
    [Display(Name = "Vekalet kaldırıldı")] DelegationRevoked = 21,
    [Display(Name = "Değişiklik yeniden gönderildi")] ChangeResubmitted = 22,
    [Display(Name = "Uygulama durumu")] ChangeImplementationUpdated = 23,
    [Display(Name = "İstek sınırı aşıldı")] RateLimited = 30
}

// Genel denetim kaydı (ISO 9001 md. 7.5.3 / ISO 27001 A.8.15). Giriş
// denemeleri LoginAttempt'te kalır; burada güvenlik ve süreç olaylarının
// ayrıntısı tutulur.
public class AuditLog
{
    public long Id { get; set; }

    public AuditAction Action { get; set; }

    // İşlemi yapan / etkilenen kullanıcı
    public int? UserId { get; set; }
    public AppUser? User { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    // İlgili talep (varsa)
    public int? TicketId { get; set; }

    [StringLength(1000)]
    public string? Detail { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(300)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = AppTime.UtcNow;
}

// ── Bildirim tercihleri ──────────────────────────────────────────────────

// Kullanıcının bir bildirim türü için hangi kanalları istediği. Satır
// yoksa NotificationChannelPolicy'deki varsayılan geçerlidir.
public class NotificationPreference
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser? User { get; set; }

    public NotificationType Type { get; set; }

    public bool InApp { get; set; } = true;
    public bool Email { get; set; }
    public bool Sms { get; set; }

    public DateTime UpdatedAt { get; set; } = AppTime.UtcNow;
}

// ── Onay vekaleti ve zincir kuralları ────────────────────────────────────

// Yönetici, belirli bir süre için onay yetkisini başka bir personele devreder.
// Vekil bu süre içinde yönetici yetkisiyle karar verir; karar kaydında
// "kimin adına" bilgisi tutulur.
public class ApprovalDelegation
{
    public int Id { get; set; }

    public int DelegatorId { get; set; }
    public AppUser? Delegator { get; set; }

    [Display(Name = "Vekil")]
    public int DelegateId { get; set; }
    public AppUser? Delegate { get; set; }

    [Display(Name = "Başlangıç")]
    public DateTime StartsAt { get; set; }

    [Display(Name = "Bitiş")]
    public DateTime EndsAt { get; set; }

    [StringLength(300), Display(Name = "Gerekçe")]
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = AppTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    public bool IsActiveAt(DateTime nowUtc) =>
        RevokedAt == null && StartsAt <= nowUtc && nowUtc < EndsAt;
}

// Onay zincirinin risk dışındaki ölçütlerle de yapılandırılması: kategori,
// öncelik ve risk koşulları. Bir talebe uyan tüm aktif kurallardan en çok
// adım isteyeni uygulanır; hiçbiri uymazsa risk bazlı varsayılan kalır.
public class ApprovalChainRule
{
    public int Id { get; set; }

    [Required, StringLength(100), Display(Name = "Kural adı")]
    public string Name { get; set; } = "";

    // Boş alan "her değer" demektir
    [Display(Name = "Kategori")]
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    [Display(Name = "En az öncelik")]
    public Priority? MinPriority { get; set; }

    [Display(Name = "Risk")]
    public RiskLevel? Risk { get; set; }

    [Range(1, 5), Display(Name = "Onay adımı")]
    public int RequiredSteps { get; set; } = 1;

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}

// Uygulama / geri alma durumu (onay sonrası)
public enum ImplementationStatus
{
    [Display(Name = "Başlamadı")] NotStarted = 0,
    [Display(Name = "Uygulanıyor")] InProgress = 1,
    [Display(Name = "Uygulandı")] Implemented = 2,
    [Display(Name = "Başarısız")] Failed = 3,
    [Display(Name = "Geri alındı")] RolledBack = 4
}
