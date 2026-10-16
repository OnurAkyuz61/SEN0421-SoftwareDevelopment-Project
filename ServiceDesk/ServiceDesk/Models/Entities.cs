using System.ComponentModel.DataAnnotations;
using ServiceDesk.Services;

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

    // Talebi açan kullanıcı. Oturumdan doldurulur, formdan gelmez.
    [Display(Name = "Talep Eden")]
    public int RequesterId { get; set; }
    public AppUser? Requester { get; set; }

    // Talep anındaki ad soyad kopyası: kullanıcı sonradan isim değiştirse veya
    // pasifleşse bile listelerde talebin kime ait olduğu görünsün diye saklanır.
    [Required, StringLength(100), Display(Name = "Talep Eden")]
    public string RequesterName { get; set; } = "";

    // Talebi üstlenen destek personeli
    [Display(Name = "Atanan")]
    public int? AssigneeId { get; set; }
    public AppUser? Assignee { get; set; }

    // Onay (sadece Değişiklik taleplerinde)
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.NotRequired;
    [StringLength(500)]
    public string? ApprovalNote { get; set; }
    public DateTime? ApprovalDecidedAt { get; set; }

    // Tüm tarihler UTC saklanır; ekranda AppTime.ToLocal ile gösterilir
    public DateTime CreatedAt { get; set; } = AppTime.UtcNow;
    public DateTime SlaDueAt { get; set; }

    // Çözüm ile kapanış ayrı olaylardır: MTTR çözüm süresinden, kapanış süresi ise
    // kullanıcının onayladığı ana kadar geçen süreden hesaplanır
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Kaç kez yeniden açıldı: ISO 9001 sürekli iyileştirmenin temel göstergelerinden
    public int ReopenCount { get; set; }

    public bool IsEscalated { get; set; }

    // Aynı talebi iki kişi aynı anda güncellerse ikincisi uyarı alır
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public Feedback? Feedback { get; set; }
    public List<Attachment> Attachments { get; set; } = new();
    public List<TicketComment> Comments { get; set; } = new();
    public List<TicketHistory> History { get; set; } = new();

    // SLA ihlali: çözüm süresi hedef süreyi aştıysa
    public bool IsSlaBreached => (ResolvedAt ?? AppTime.UtcNow) > SlaDueAt;
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

    public DateTime CreatedAt { get; set; } = AppTime.UtcNow;
}

// Talep üzerindeki yazışma. IsInternal = true olan notlar kullanıcıya gösterilmez.
public class TicketComment
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public int AuthorId { get; set; }
    public AppUser? Author { get; set; }

    [Required(ErrorMessage = "Mesaj boş olamaz"), StringLength(4000), Display(Name = "Mesaj")]
    public string Body { get; set; } = "";

    // Yalnızca destek ekibinin gördüğü iç not
    [Display(Name = "İç not")]
    public bool IsInternal { get; set; }

    // Bu not için harcanan efor; personel verimliliği göstergesinin girdisi
    [Range(0, 10000), Display(Name = "Harcanan süre (dk)")]
    public int? WorkMinutes { get; set; }

    public DateTime CreatedAt { get; set; } = AppTime.UtcNow;

    public List<Attachment> Attachments { get; set; } = new();
}

// Talep üzerinde yapılan her alan değişikliğinin izi (ISO 9001 md. 7.5.3).
// AppDbContext.SaveChangesAsync tarafından otomatik yazılır.
public class TicketHistory
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    [Required, StringLength(50)]
    public string Field { get; set; } = "";

    [StringLength(100)] public string? OldValue { get; set; }
    [StringLength(100)] public string? NewValue { get; set; }

    // Değişikliği yapan kullanıcı; arka plan servisi yazdıysa boş kalır
    public int? ChangedById { get; set; }
    public AppUser? ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; } = AppTime.UtcNow;
}

// Talebe eklenen dosya / ekran görüntüsü (dosyanın kendisi Uploads klasöründe durur)
public class Attachment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    // Dosya bir yazışma mesajına eklendiyse o mesaja bağlanır
    public int? CommentId { get; set; }
    public TicketComment? Comment { get; set; }

    public int? UploadedById { get; set; }
    public AppUser? UploadedBy { get; set; }

    [Required, StringLength(255)]
    public string FileName { get; set; } = "";        // kullanıcının yüklediği orijinal ad

    [Required, StringLength(100)]
    public string StoredFileName { get; set; } = "";  // diskteki ad (guid + uzantı)

    [StringLength(100)]
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; } = AppTime.UtcNow;

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

    // Telefonsuz kullanıcı da eklenebilsin diye opsiyonel; benzersizlik filtreli index ile korunur
    [StringLength(20), Display(Name = "Telefon")]
    public string? PhoneNumber { get; set; }

    [Required, StringLength(150), EmailAddress, Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    // Şifrenin kendisi saklanmaz; PasswordHasher<AppUser> ile üretilen özet tutulur
    [Required, StringLength(256)]
    public string PasswordHash { get; set; } = "";

    [Display(Name = "Rol")]
    public UserRole Role { get; set; } = UserRole.User;

    // Ayrılan kullanıcı silinmez, pasifleştirilir; talep geçmişi kopmaz
    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    [Display(Name = "Ad Soyad")]
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public enum OtpDeliveryChannel { Sms, WhatsApp }
