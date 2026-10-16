using System.ComponentModel.DataAnnotations;
using ServiceDesk.Services;

namespace ServiceDesk.Models;

public enum TicketType { [Display(Name = "Arıza")] Incident, [Display(Name = "Değişiklik")] Change }
public enum Priority { [Display(Name = "Düşük")] Low, [Display(Name = "Orta")] Medium, [Display(Name = "Yüksek")] High, [Display(Name = "Kritik")] Critical }
public enum TicketStatus { New, InProgress, Resolved, Closed }

// Kurum birimi: talepler ve göstergeler birim bazında kırılabilsin diye
public class Department
{
    public int Id { get; set; }

    [Required, StringLength(100), Display(Name = "Birim")]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public List<Team> Teams { get; set; } = new();
}

// Destek ekibi: talep kişiye atanmadan önce ekibe düşer
public class Team
{
    public int Id { get; set; }

    [Required, StringLength(100), Display(Name = "Ekip")]
    public string Name { get; set; } = "";

    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    // Eskalasyonun ikinci seviyesi ileride bu kişiye yükselecek
    public int? LeadUserId { get; set; }
    public AppUser? LeadUser { get; set; }

    public bool IsActive { get; set; } = true;
}

// Talep kategorisi. Enum yerine tablo: yeni kategori eklemek artık
// kod değişikliği ve migration gerektirmiyor, alt kategori kurulabiliyor.
public class Category
{
    public int Id { get; set; }

    [Required, StringLength(100), Display(Name = "Kategori")]
    public string Name { get; set; } = "";

    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public List<Category> Children { get; set; } = new();

    // Bu kategorideki talepler varsayılan olarak hangi ekibe düşer
    public int? DefaultTeamId { get; set; }
    public Team? DefaultTeam { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

// Değişiklik (Change) talepleri yönetici onayından geçer
public enum ApprovalStatus { NotRequired = 0, Pending = 1, Approved = 2, Rejected = 3 }

// Değişikliğin taşıdığı risk; onay adımı sayısını belirler
public enum RiskLevel
{
    [Display(Name = "Düşük")] Low = 0,
    [Display(Name = "Orta")] Medium = 1,
    [Display(Name = "Yüksek")] High = 2
}

// Değişiklik talebinin planı. Change'i Incident'tan ayıran bilgi burada:
// risk, ne zaman uygulanacağı, nasıl uygulanacağı ve ters giderse nasıl geri alınacağı.
// Talep ile bire bir; birincil anahtar talebin kendisidir.
public class ChangeDetail
{
    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    [Display(Name = "Risk")]
    public RiskLevel Risk { get; set; } = RiskLevel.Low;

    [Display(Name = "Planlanan başlangıç")]
    public DateTime? PlannedStart { get; set; }

    [Display(Name = "Planlanan bitiş")]
    public DateTime? PlannedEnd { get; set; }

    [StringLength(4000), Display(Name = "Uygulama planı")]
    public string? ImplementationPlan { get; set; }

    [StringLength(4000), Display(Name = "Geri alma planı")]
    public string? RollbackPlan { get; set; }

    [StringLength(4000), Display(Name = "Etki analizi")]
    public string? ImpactAnalysis { get; set; }

    public DateTime UpdatedAt { get; set; } = AppTime.UtcNow;
    public int? UpdatedById { get; set; }
    public AppUser? UpdatedBy { get; set; }
}

// Onay zincirindeki her karar bir satır: kim, hangi adımda, ne zaman, ne dedi.
// Yüksek riskli değişiklikler iki adımdan geçer (önce destek, sonra yönetici).
public class ChangeApproval
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    [Display(Name = "Adım")]
    public int Step { get; set; }

    public int ApproverId { get; set; }
    public AppUser? Approver { get; set; }

    public ApprovalStatus Decision { get; set; }
    public DateTime DecidedAt { get; set; } = AppTime.UtcNow;

    [StringLength(500), Display(Name = "Not")]
    public string? Note { get; set; }
}

// Destek talebi (Incident / Change)
public class Ticket
{
    public int Id { get; set; }

    // Kullanıcıya ve yazışmalara gösterilen numara: INC-2026-00042 / CHG-2026-00007.
    // Kayıt oluştuktan sonra Id'den türetilir, bu yüzden ilk anda boş olabilir.
    [StringLength(20), Display(Name = "Talep No")]
    public string? Number { get; set; }

    [Required, StringLength(150), Display(Name = "Başlık")]
    public string Title { get; set; } = "";

    [Required, Display(Name = "Açıklama")]
    public string Description { get; set; } = "";

    [Display(Name = "Tür")]
    public TicketType Type { get; set; }

    [Display(Name = "Öncelik")]
    public Priority Priority { get; set; } = Priority.Medium;

    [Display(Name = "Kategori")]
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    // Kişiye atanmadan önce talebin düştüğü ekip
    [Display(Name = "Ekip")]
    public int? AssignedTeamId { get; set; }
    public Team? AssignedTeam { get; set; }

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

    // Hangi SLA politikası uygulandı: politika sonradan değişse bile
    // bu talebin hangi taahhüde göre ölçüldüğü kayıtlı kalır
    public int? SlaPolicyId { get; set; }
    public SlaPolicy? SlaPolicy { get; set; }

    // Tüm tarihler UTC saklanır; ekranda AppTime.ToLocal ile gösterilir
    public DateTime CreatedAt { get; set; } = AppTime.UtcNow;

    // SLA'nın iki ayağı: ilk yanıt ve çözüm
    public DateTime? FirstResponseDueAt { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime SlaDueAt { get; set; }

    // Çözüm ile kapanış ayrı olaylardır: MTTR çözüm süresinden, kapanış süresi ise
    // kullanıcının onayladığı ana kadar geçen süreden hesaplanır
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Kaç kez yeniden açıldı: ISO 9001 sürekli iyileştirmenin temel göstergelerinden
    public int ReopenCount { get; set; }

    // İhlal artık her sayfa açılışında hesaplanmıyor; arka plan servisi
    // tespit ettiği anda buraya yazıyor. Böylece SQL tarafında filtrelenebiliyor
    // ve "ne zaman ihlal edildi" sorusunun cevabı kayıtlı oluyor.
    public bool IsSlaBreached { get; set; }
    public DateTime? SlaBreachedAt { get; set; }
    public DateTime? SlaWarningSentAt { get; set; }

    public bool IsEscalated { get; set; }
    public int EscalationLevel { get; set; }

    // Kayıt silinmez, pasifleştirilir (ISO 9001 md. 7.5: kayıtların korunması).
    // Global query filter sayesinde silinmiş talep sorgulara hiç düşmez.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }
    public AppUser? DeletedBy { get; set; }

    // Aynı talebi iki kişi aynı anda güncellerse ikincisi uyarı alır
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public Feedback? Feedback { get; set; }
    public List<Attachment> Attachments { get; set; } = new();
    public List<TicketComment> Comments { get; set; } = new();
    public List<TicketHistory> History { get; set; } = new();
    public List<EscalationRecord> Escalations { get; set; } = new();

    // Yalnızca Değişiklik taleplerinde dolu olur
    public ChangeDetail? ChangeDetail { get; set; }
    public List<ChangeApproval> Approvals { get; set; } = new();

    // Yüksek riskli değişiklik iki onaydan geçer, diğerleri birden
    public int RequiredApprovalSteps =>
        Type == TicketType.Change && ChangeDetail?.Risk == RiskLevel.High ? 2 : 1;

    // Henüz çözülmemiş ve süresi dolmuş mu (ekranda anlık uyarı için).
    // Kalıcı ihlal bilgisi IsSlaBreached kolonunda.
    public bool IsOverdueNow => ResolvedAt == null && AppTime.UtcNow > SlaDueAt;
}

// Hizmet seviyesi taahhüdü: önceliğe (ve isteğe bağlı olarak talep türüne) göre
// ilk yanıt ve çözüm süresi. Yürürlük tarihiyle sürümlenir; eski talepler
// kendi uygulandıkları politikaya bağlı kalır.
public class SlaPolicy
{
    public int Id { get; set; }

    [Required, StringLength(100), Display(Name = "Politika")]
    public string Name { get; set; } = "";

    public Priority Priority { get; set; }

    // null = her tür için geçerli
    public TicketType? AppliesToType { get; set; }

    [Display(Name = "İlk yanıt (dk)")]
    public int FirstResponseMinutes { get; set; }

    [Display(Name = "Çözüm (dk)")]
    public int ResolutionMinutes { get; set; }

    // true ise süre yalnızca mesai saatlerinde işler
    [Display(Name = "Mesai saatleriyle")]
    public bool BusinessHoursOnly { get; set; } = true;

    public int? BusinessCalendarId { get; set; }
    public BusinessCalendar? BusinessCalendar { get; set; }

    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

// Mesai takvimi: SLA sayacı yalnızca çalışma saatlerinde ilerler
public class BusinessCalendar
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    // Gün içindeki çalışma aralığı, gece yarısından itibaren dakika (09:00 = 540)
    public int WorkStartMinutes { get; set; } = 9 * 60;
    public int WorkEndMinutes { get; set; } = 18 * 60;

    // Çalışılan günler: "1,2,3,4,5" = Pazartesi–Cuma (ISO gün numarası)
    [Required, StringLength(20)]
    public string WorkDays { get; set; } = "1,2,3,4,5";

    public List<Holiday> Holidays { get; set; } = new();
}

// Resmî tatil: bu günlerde SLA sayacı durur
public class Holiday
{
    public int Id { get; set; }
    public int BusinessCalendarId { get; set; }
    public BusinessCalendar? BusinessCalendar { get; set; }

    public DateTime Date { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = "";
}

// Eskalasyon kuralı: süresi dolan talep hangi gecikmede hangi role yükselir
public class EscalationRule
{
    public int Id { get; set; }

    [Display(Name = "Seviye")]
    public int Level { get; set; }

    // SLA bitişinden kaç dakika sonra devreye girer (0 = ihlal anında)
    [Display(Name = "Gecikme (dk)")]
    public int AfterMinutes { get; set; }

    [Display(Name = "Hedef rol")]
    public UserRole TargetRole { get; set; }

    public bool IsActive { get; set; } = true;
}

// Gerçekleşen eskalasyon kaydı: bool bayrak değil, olayın kendisi
public class EscalationRecord
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public int Level { get; set; }
    public DateTime OccurredAt { get; set; } = AppTime.UtcNow;

    // Kaç dakika gecikmeyle tespit edildi
    public int MinutesOverdue { get; set; }

    // Kime yükseltildi (rol bazlı kuralda birden fazla kişiye bildirim gider,
    // atanan kişi varsa o yazılır)
    public int? EscalatedToId { get; set; }
    public AppUser? EscalatedTo { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }
}

public enum NotificationType
{
    [Display(Name = "SLA yaklaşıyor")] SlaWarning = 0,
    [Display(Name = "SLA ihlali")] SlaBreach = 1,
    [Display(Name = "Eskalasyon")] Escalation = 2,
    [Display(Name = "Talep atandı")] Assigned = 3,
    [Display(Name = "Onay bekliyor")] ApprovalPending = 4,
    [Display(Name = "Onay kararı")] ApprovalDecided = 5,
    [Display(Name = "Yeni mesaj")] NewComment = 6
}

public enum NotificationChannel { InApp = 0, Email = 1, Sms = 2 }

// Gönderilen uyarının kaydı. Önceden bildirimler her sayfa açılışında
// hesaplanıyordu; artık kime, ne zaman gittiği ve okunup okunmadığı kayıtlı.
public class Notification
{
    public int Id { get; set; }

    public int RecipientId { get; set; }
    public AppUser? Recipient { get; set; }

    public int? TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    [Required, StringLength(200)]
    public string Title { get; set; } = "";

    [Required, StringLength(500)]
    public string Message { get; set; } = "";

    public DateTime CreatedAt { get; set; } = AppTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    // Uygulama içi bildirim anında "gönderilmiş" sayılır; e-posta/SMS
    // entegrasyonu bağlanınca bu alanlar gerçek gönderimi kaydedecek
    public bool IsSent { get; set; } = true;
    public DateTime? SentAt { get; set; } = AppTime.UtcNow;

    [StringLength(300)]
    public string? SendError { get; set; }
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

    [Display(Name = "Birim")]
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    [Display(Name = "Ekip")]
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    // Ayrılan kullanıcı silinmez, pasifleştirilir; talep geçmişi kopmaz
    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    [Display(Name = "Ad Soyad")]
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public enum OtpDeliveryChannel { Sms, WhatsApp }
