using System.ComponentModel.DataAnnotations;

namespace ServiceDesk.Models;

public enum TicketType { [Display(Name = "Arıza")] Incident, [Display(Name = "Değişiklik")] Change }
public enum Priority { [Display(Name = "Düşük")] Low, [Display(Name = "Orta")] Medium, [Display(Name = "Yüksek")] High, [Display(Name = "Kritik")] Critical }
public enum TicketStatus { New, InProgress, Resolved, Closed }

// Sorun kategorisi (mevcut kayıtlar "Diğer" olur)
public enum TicketCategory
{
    [Display(Name = "Diğer")] Other = 0,
    [Display(Name = "Donanım")] Hardware = 1,
    [Display(Name = "Yazılım")] Software = 2,
    [Display(Name = "Ağ / İnternet")] Network = 3,
    [Display(Name = "Hesap / Erişim")] Access = 4,
    [Display(Name = "E-posta")] Email = 5
}

// Değişiklik (Change) talepleri yönetici onayından geçer
public enum ApprovalStatus { NotRequired = 0, Pending = 1, Approved = 2, Rejected = 3 }

// Destek talebi (Incident / Change)
public class Ticket
{
    public int Id { get; set; }

    [Required, StringLength(150), Display(Name = "Başlık")]
    public string Title { get; set; } = "";

    [Required, Display(Name = "Açıklama")]
    public string Description { get; set; } = "";

    [Display(Name = "Tür")]
    public TicketType Type { get; set; }

    [Display(Name = "Öncelik")]
    public Priority Priority { get; set; } = Priority.Medium;

    [Display(Name = "Kategori")]
    public TicketCategory Category { get; set; } = TicketCategory.Other;

    [Display(Name = "Durum")]
    public TicketStatus Status { get; set; } = TicketStatus.New;

    [Required, StringLength(100), Display(Name = "Talep Eden")]
    public string RequesterName { get; set; } = "";

    // Login bağlanınca oturumdaki kullanıcı buraya yazılacak (şimdilik boş kalabilir)
    public int? RequesterId { get; set; }
    public AppUser? Requester { get; set; }

    [StringLength(100), Display(Name = "Atanan")]
    public string? AssignedTo { get; set; }

    // Onay (sadece Değişiklik taleplerinde)
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.NotRequired;
    [StringLength(500)]
    public string? ApprovalNote { get; set; }
    public DateTime? ApprovalDecidedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime SlaDueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsEscalated { get; set; }

    public Feedback? Feedback { get; set; }
    public List<Attachment> Attachments { get; set; } = new();

    // SLA ihlali: çözüm süresi hedef süreyi aştıysa
    public bool IsSlaBreached => (ResolvedAt ?? DateTime.Now) > SlaDueAt;
}

// Önceliğe göre SLA çözüm süresi (saat)
public class SlaPolicy
{
    public int Id { get; set; }
    public Priority Priority { get; set; }
    public int ResolutionHours { get; set; }
}

// Müşteri memnuniyet anketi (1-5)
public class Feedback
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    [Range(1, 5), Display(Name = "Puan")]
    public int Score { get; set; }

    [StringLength(500), Display(Name = "Yorum")]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// Talebe eklenen dosya / ekran görüntüsü (dosyanın kendisi Uploads klasöründe durur)
public class Attachment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    [Required, StringLength(255)]
    public string FileName { get; set; } = "";        // kullanıcının yüklediği orijinal ad

    [Required, StringLength(100)]
    public string StoredFileName { get; set; } = "";  // diskteki ad (guid + uzantı)

    [StringLength(100)]
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.Now;

    public bool IsImage => ContentType.StartsWith("image/");
}

public enum UserRole
{
    [Display(Name = "Kullanıcı")] User = 0,
    [Display(Name = "Destek")] Agent = 1,
    [Display(Name = "Yönetici")] Admin = 2
}

// Giriş + OTP için kullanıcı
public class AppUser
{
    public int Id { get; set; }

    [Required, StringLength(50), Display(Name = "Ad")]
    public string FirstName { get; set; } = "";

    [Required, StringLength(50), Display(Name = "Soyad")]
    public string LastName { get; set; } = "";

    [Required, StringLength(20), Display(Name = "Telefon")]
    public string PhoneNumber { get; set; } = "";

    [Required, StringLength(150), EmailAddress, Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [Required, StringLength(100), Display(Name = "Şifre")]
    public string Password { get; set; } = "";

    [Display(Name = "Rol")]
    public UserRole Role { get; set; } = UserRole.User;

    [Display(Name = "Ad Soyad")]
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public enum OtpDeliveryChannel { Sms, WhatsApp }
