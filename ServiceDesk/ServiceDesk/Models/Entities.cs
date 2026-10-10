using System.ComponentModel.DataAnnotations;

namespace ServiceDesk.Models;

public enum TicketType { [Display(Name = "Arıza")] Incident, [Display(Name = "Değişiklik")] Change }
public enum Priority { [Display(Name = "Düşük")] Low, [Display(Name = "Orta")] Medium, [Display(Name = "Yüksek")] High, [Display(Name = "Kritik")] Critical }
public enum TicketStatus { New, InProgress, Resolved, Closed }

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

    [Display(Name = "Durum")]
    public TicketStatus Status { get; set; } = TicketStatus.New;

    [Required, StringLength(100), Display(Name = "Talep Eden")]
    public string RequesterName { get; set; } = "";

    [StringLength(100), Display(Name = "Atanan")]
    public string? AssignedTo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime SlaDueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsEscalated { get; set; }

    public Feedback? Feedback { get; set; }

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
