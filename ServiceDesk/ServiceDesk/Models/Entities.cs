using System.ComponentModel.DataAnnotations;
using ServiceDesk.Services;

namespace ServiceDesk.Models;

public enum TicketType { [Display(Name = "Arıza")] Incident, [Display(Name = "Değişiklik")] Change }
public enum Priority { [Display(Name = "Düşük")] Low, [Display(Name = "Orta")] Medium, [Display(Name = "Yüksek")] High, [Display(Name = "Kritik")] Critical }
// WaitingForCustomer: kullanıcıdan bilgi bekleniyor, SLA sayacı durur.
// Sona eklendi ki mevcut kayıtların sayısal değerleri değişmesin.
public enum TicketStatus { New, InProgress, Resolved, Closed, WaitingForCustomer }

// Talep çözülürken / kapatılırken seçilen zorunlu çözüm kodu
public enum ResolutionCode
{
    [Display(Name = "Kalıcı çözüm")] Fixed = 0,
    [Display(Name = "Geçici çözüm")] Workaround = 1,
    [Display(Name = "Bilgi verildi")] Information = 2,
    [Display(Name = "Kullanıcı hatası")] UserError = 3,
    [Display(Name = "Tekrarlanamadı")] CannotReproduce = 4,
    [Display(Name = "Değişiklik uygulandı")] ChangeImplemented = 5,
    [Display(Name = "Mükerrer (birleştirildi)")] Duplicate = 6,
    [Display(Name = "İptal edildi")] Cancelled = 7
}

// Kurum birimi: talepler ve göstergeler birim bazında kırılabilsin diye
public class Department
{
    public int Id { get; set; }

    [Required, StringLength(100), Display(Name = "Birim")]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;

    // Birime özel mesai takvimi. Boşsa SLA politikasının takvimi kullanılır.
    public int? BusinessCalendarId { get; set; }
    public BusinessCalendar? BusinessCalendar { get; set; }

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

    // Round-robin: ekibe düşen yeni talep sıradaki aktif personele otomatik atanır
    public bool RoundRobinEnabled { get; set; }
    public int? LastAssignedUserId { get; set; }
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

    // Yüksek riskli değişiklikler için CAB (Change Advisory Board) toplantı kaydı.
    // Nihai onay, ayar açıksa bu bağlantı girilmeden verilemez (ApprovalPolicy).
    [StringLength(500), Display(Name = "CAB toplantı kaydı")]
    public string? CabMeetingUrl { get; set; }

    [Display(Name = "CAB toplantı tarihi")]
    public DateTime? CabMeetingAt { get; set; }

    // Onay sonrası uygulama / geri alma durumu (ChangeImplementationRules)
    [Display(Name = "Uygulama durumu")]
    public ImplementationStatus ImplementationStatus { get; set; } = ImplementationStatus.NotStarted;
    public DateTime? ImplementationStartedAt { get; set; }
    public DateTime? ImplementedAt { get; set; }
    public DateTime? RolledBackAt { get; set; }

    [StringLength(1000), Display(Name = "Uygulama notu")]
    public string? ImplementationNote { get; set; }

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

    // Reddedilip düzeltilerek yeniden onaya gönderilen talepte her gönderim
    // yeni bir tur açar; önceki turun kararları geçmiş olarak kalır
    [Display(Name = "Tur")]
    public int Round { get; set; } = 1;

    [Display(Name = "Adım")]
    public int Step { get; set; }

    public int ApproverId { get; set; }
    public AppUser? Approver { get; set; }

    // Vekaleten verilen kararda yetkiyi devreden yönetici
    public int? OnBehalfOfId { get; set; }
    public AppUser? OnBehalfOf { get; set; }

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

    // Kaçıncı onay turu (1 = ilk gönderim; her yeniden gönderimde artar)
    public int ApprovalRound { get; set; } = 1;

    // Onay zinciri kuralına (ApprovalChainRule) göre çözümlenmiş adım sayısı.
    // Boşsa risk bazlı varsayılan uygulanır (yüksek risk 2, diğerleri 1).
    public int? ApprovalStepsRequired { get; set; }

    // Onay bekleyen değişiklik için son hatırlatmanın zamanı (ApprovalReminderService)
    public DateTime? LastApprovalReminderAt { get; set; }

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

    // İlk yanıt süresinin aşılması çözüm ihlalinden ayrı işaretlenir
    public bool IsFirstResponseBreached { get; set; }
    public DateTime? FirstResponseBreachedAt { get; set; }

    // "Müşteri bekleniyor" durumunda SLA sayacı durur. Duraklama başlangıcı ve
    // toplam duraklanan mesai dakikası tutulur; öncelik değişince yeniden
    // hesaplamada bu süre hedefe eklenir.
    public DateTime? SlaPausedAt { get; set; }
    public int SlaPausedMinutes { get; set; }

    // Çözüm / kapanışta zorunlu
    [Display(Name = "Çözüm kodu")]
    public ResolutionCode? ResolutionCode { get; set; }

    [StringLength(1000), Display(Name = "Kök neden")]
    public string? RootCause { get; set; }

    // Portaldan gelen iptal isteği; destek onaylarsa talep kapanır
    public DateTime? CancelRequestedAt { get; set; }

    [StringLength(500), Display(Name = "İptal nedeni")]
    public string? CancelReason { get; set; }

    // Mükerrer talep birleştirildiyse asıl talep
    public int? MergedIntoTicketId { get; set; }

    // Virgülle ayrılmış etiketler: "vpn,laptop,acil"
    [StringLength(300), Display(Name = "Etiketler")]
    public string? Tags { get; set; }

    // Yazışmalarda girilen harcanan sürelerin talep bazında toplamı (dk)
    [Display(Name = "Toplam efor (dk)")]
    public int WorkMinutesTotal { get; set; }

    // Kayıt silinmez, pasifleştirilir (ISO 9001 md. 7.5: kayıtların korunması).
    // Global query filter sayesinde silinmiş talep sorgulara hiç düşmez.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }
    public AppUser? DeletedBy { get; set; }

    // Talep neden arşivlendi: denetimde "bu kayıt neden listeden kalktı" sorusunun cevabı
    [StringLength(500), Display(Name = "Arşivleme nedeni")]
    public string? DeleteReason { get; set; }

    // Aynı talebi iki kişi aynı anda güncellerse ikincisi uyarı alır
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public SurveyResponse? Survey { get; set; }

    // Talep bir bilgi bankası makalesiyle çözüldüyse
    public int? ResolvedByArticleId { get; set; }
    public KnowledgeArticle? ResolvedByArticle { get; set; }
    public List<Attachment> Attachments { get; set; } = new();
    public List<TicketComment> Comments { get; set; } = new();
    public List<TicketHistory> History { get; set; } = new();
    public List<EscalationRecord> Escalations { get; set; } = new();
    public List<TicketFollower> Followers { get; set; } = new();

    public IEnumerable<string> TagList =>
        (Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Yalnızca Değişiklik taleplerinde dolu olur
    public ChangeDetail? ChangeDetail { get; set; }
    public List<ChangeApproval> Approvals { get; set; } = new();

    // Zincir kuralı çözümlendiyse o, yoksa risk bazlı varsayılan:
    // yüksek riskli değişiklik iki onaydan geçer, diğerleri birden
    public int RequiredApprovalSteps =>
        ApprovalStepsRequired is > 0 ? ApprovalStepsRequired.Value : DefaultApprovalSteps;

    public int DefaultApprovalSteps =>
        Type == TicketType.Change && ChangeDetail?.Risk == RiskLevel.High ? 2 : 1;

    // Geçerli turdaki onay kararları
    public IEnumerable<ChangeApproval> CurrentRoundApprovals =>
        Approvals.Where(a => a.Round == ApprovalRound);

    // Henüz çözülmemiş ve süresi dolmuş mu (ekranda anlık uyarı için).
    // Kalıcı ihlal bilgisi IsSlaBreached kolonunda.
    public bool IsOverdueNow => ResolvedAt == null && SlaPausedAt == null && AppTime.UtcNow > SlaDueAt;
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

    // SLA bitimine kaç dakika kala uyarı gönderilir
    [Display(Name = "Uyarı eşiği (dk)")]
    public int WarningMinutesBefore { get; set; } = 120;

    // Sürümleme: politika güncellenince eski satır kapanır (EffectiveTo),
    // aynı kökten yeni sürüm açılır. İlk sürümde RootPolicyId boştur.
    public int Version { get; set; } = 1;
    public int? RootPolicyId { get; set; }
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

// Eskalasyon kuralı: süresi dolan talep hangi gecikmede hangi role yeniden
// atanır ve bildirilir (bkz. SlaMonitorService).
public class EscalationRule
{
    public int Id { get; set; }

    [Display(Name = "Seviye")]
    public int Level { get; set; }

    // SLA bitişinden kaç dakika sonra yeniden atama + bildirim (0 = ihlal anında)
    [Display(Name = "Gecikme (dk)")]
    public int AfterMinutes { get; set; }

    // Talebin devredileceği rol; uygun aktif kişi seçilir, role de bildirim gider
    [Display(Name = "Hedef rol")]
    public UserRole TargetRole { get; set; }

    // Hedef: roldeki en az yüklü kişi ya da talebin ekibinin lideri
    [Display(Name = "Hedef türü")]
    public EscalationTarget TargetType { get; set; } = EscalationTarget.Role;

    // Hedefte uygun kişi yoksa: önce yedek kişi, sonra yedek rol denenir
    [Display(Name = "Yedek kişi")]
    public int? FallbackUserId { get; set; }
    public AppUser? FallbackUser { get; set; }

    [Display(Name = "Yedek rol")]
    public UserRole? FallbackRole { get; set; }

    // Eskalasyon sonrası öncelik bir kademe artırılsın mı (Kritik'te durur)
    [Display(Name = "Önceliği artır")]
    public bool RaisePriority { get; set; } = true;

    public bool IsActive { get; set; } = true;
}

public enum EscalationTarget
{
    [Display(Name = "Rol")] Role = 0,
    [Display(Name = "Ekip lideri")] TeamLead = 1
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

    // Elle (endpoint üzerinden) mi tetiklendi; tetikleyen kişi
    public bool IsManual { get; set; }
    public int? TriggeredById { get; set; }
    public AppUser? TriggeredBy { get; set; }

    // Hedefte kimse bulunamayıp yedek kurala düşüldü mü
    public bool UsedFallback { get; set; }

    // Eskalasyonla öncelik değiştiyse önceki / sonraki değer
    public Priority? PriorityBefore { get; set; }
    public Priority? PriorityAfter { get; set; }
}

public enum NotificationType
{
    [Display(Name = "SLA yaklaşıyor")] SlaWarning = 0,
    [Display(Name = "SLA ihlali")] SlaBreach = 1,
    [Display(Name = "Eskalasyon")] Escalation = 2,
    [Display(Name = "Talep atandı")] Assigned = 3,
    [Display(Name = "Onay bekliyor")] ApprovalPending = 4,
    [Display(Name = "Onay kararı")] ApprovalDecided = 5,
    [Display(Name = "Yeni mesaj")] NewComment = 6,
    [Display(Name = "Yeni talep")] NewTicket = 7,
    [Display(Name = "Günlük özet")] DailyDigest = 8,
    [Display(Name = "Onay hatırlatma")] ApprovalReminder = 9,
    [Display(Name = "Değişiklik uygulaması")] ChangeImplementation = 10
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

    // Yeniden deneme kuyruğu (DeliveryRetryPolicy): kaç kez denendi, bir
    // sonraki deneme ne zaman. Sınır aşılınca IsFailed = true olur ve kayıt
    // kuyruktan çıkar (yönetici bildirim listesinde görür).
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public bool IsFailed { get; set; }

    // E-posta şablonu seçimi (EmailTemplates): "escalation.l2", "sla.breach" gibi.
    // Boşsa bildirim türünün varsayılan şablonu kullanılır.
    [StringLength(50)]
    public string? TemplateKey { get; set; }
}

// ── Müşteri memnuniyeti (ISO 9001 md. 9.1.2) ─────────────────────────────
// Tek puanlı Feedback yerine şablonlu anket: yönetici soruları düzenleyebilir,
// hangi boyutta memnuniyetsizlik olduğu ayrıştırılabilir.

public class SurveyTemplate
{
    public int Id { get; set; }

    [Required, StringLength(100), Display(Name = "Anket")]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public List<SurveyQuestion> Questions { get; set; } = new();
}

public class SurveyQuestion
{
    public int Id { get; set; }

    public int TemplateId { get; set; }
    public SurveyTemplate? Template { get; set; }

    [Required, StringLength(200), Display(Name = "Soru")]
    public string Text { get; set; } = "";

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

// Bir talebe verilen tek anket cevabı (talep başına en fazla bir tane)
public class SurveyResponse
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public int TemplateId { get; set; }
    public SurveyTemplate? Template { get; set; }

    // Anketi kimin doldurduğu artık kayıtlı
    public int SubmittedById { get; set; }
    public AppUser? SubmittedBy { get; set; }

    [StringLength(500), Display(Name = "Yorum")]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = AppTime.UtcNow;

    public List<SurveyAnswer> Answers { get; set; } = new();

    // Genel memnuniyet: soruların ortalaması
    public double Average => Answers.Count == 0 ? 0 : Answers.Average(a => a.Score);
}

public class SurveyAnswer
{
    public int Id { get; set; }

    public int ResponseId { get; set; }
    public SurveyResponse? Response { get; set; }

    public int QuestionId { get; set; }
    public SurveyQuestion? Question { get; set; }

    [Range(1, 5), Display(Name = "Puan")]
    public int Score { get; set; }
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
    [Range(1, 10000), Display(Name = "Harcanan süre (dk)")]
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

    // Tarayıcıda gösterilebilen güvenli görsel mi? Kaydedilen içerik türüne değil
    // (eski kayıtlarda istemciden geliyordu) diskteki dosyanın uzantısına bakılır.
    public bool IsImage => UploadPolicy.IsInlineImage(StoredFileName);
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

    // Günlük özet e-postası (DailyDigestService)
    [Display(Name = "Günlük özet e-postası")]
    public bool DailyDigestEnabled { get; set; } = true;
    public DateTime? LastDigestSentAt { get; set; }

    [Display(Name = "Ad Soyad")]
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public enum OtpDeliveryChannel { Sms, WhatsApp }

public enum LoginResult
{
    [Display(Name = "Başarılı")] Success = 0,
    [Display(Name = "Şifre hatalı")] WrongPassword = 1,
    [Display(Name = "Hesap bulunamadı")] UnknownEmail = 2,
    [Display(Name = "Telefon tanımsız")] NoPhone = 3,
    [Display(Name = "OTP hatalı")] OtpFailed = 4,
    [Display(Name = "OTP süresi doldu")] OtpExpired = 5,
    [Display(Name = "Çok fazla deneme")] LockedOut = 6,
    // Yalnızca Development'ta, şifre ve OTP atlanarak yapılan giriş
    [Display(Name = "Geliştirme girişi")] DevLogin = 7
}

// Giriş denemesi denetim izi. Hem güvenlik kaydı hem de deneme
// sınırlamasının dayandığı veri: art arda başarısız denemeler sayılır.
public class LoginAttempt
{
    public int Id { get; set; }

    public int? UserId { get; set; }
    public AppUser? User { get; set; }

    [Required, StringLength(150)]
    public string Email { get; set; } = "";

    [StringLength(45)]
    public string? IpAddress { get; set; }

    public LoginResult Result { get; set; }
    public DateTime AttemptedAt { get; set; } = AppTime.UtcNow;

    public bool IsSuccess => Result == LoginResult.Success;
}

// Şifre sıfırlama bağlantısı. Ham token e-postada gider; veritabanında
// yalnızca SHA-256 özeti tutulur. Tek kullanımlık ve sürelidir.
public class PasswordResetToken
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser? User { get; set; }

    [Required, StringLength(64)]
    public string TokenHash { get; set; } = "";

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = AppTime.UtcNow;
    public DateTime? UsedAt { get; set; }

    public bool IsUsable => UsedAt == null && AppTime.UtcNow <= ExpiresAt;
}

// ── ISO 9001 kalite katmanı ──────────────────────────────────────────────

public enum KpiPeriod
{
    [Display(Name = "Günlük")] Daily = 0,
    [Display(Name = "Aylık")] Monthly = 1
}

// Dönemsel ölçüm kaydı (md. 9.1.1). Göstergeler her panel açılışında
// yeniden hesaplanmak yerine dönem kapanınca dondurulur; böylece
// "geçen aya göre iyileşme" sorusu cevaplanabilir.
public class KpiSnapshot
{
    public int Id { get; set; }

    public KpiPeriod Period { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // null = kurum geneli
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public int TotalTickets { get; set; }
    public int ResolvedTickets { get; set; }
    public int BreachedTickets { get; set; }
    public int ReopenCount { get; set; }

    // İlk yanıt SLA'sı çözüm SLA'sından ayrı ölçülür
    public int FirstResponseBreachedTickets { get; set; }

    [Display(Name = "İlk yanıt SLA uyumu (%)")]
    public decimal FirstResponseComplianceRate { get; set; }

    [Display(Name = "SLA uyumu (%)")]
    public decimal SlaComplianceRate { get; set; }

    [Display(Name = "Ortalama çözüm (saat)")]
    public decimal AvgResolutionHours { get; set; }

    [Display(Name = "Ortalama ilk yanıt (saat)")]
    public decimal AvgFirstResponseHours { get; set; }

    [Display(Name = "Memnuniyet")]
    public decimal CsatAverage { get; set; }

    public DateTime CalculatedAt { get; set; } = AppTime.UtcNow;
}

public enum NonconformitySource
{
    [Display(Name = "SLA ihlali")] SlaBreach = 0,
    [Display(Name = "Düşük memnuniyet")] LowSatisfaction = 1,
    [Display(Name = "İç denetim")] Audit = 2,
    [Display(Name = "Elle açıldı")] Manual = 3
}

public enum NonconformityStatus
{
    [Display(Name = "Açık")] Open = 0,
    [Display(Name = "İşlemde")] InProgress = 1,
    [Display(Name = "Kapalı")] Closed = 2
}

// Uygunsuzluk ve düzeltici faaliyet (md. 10.2). Projeyi sıradan bir talep
// sisteminden ayıran kayıt: kök neden, alınan önlem, sorumlu, etkinlik kontrolü.
public class Nonconformity
{
    public int Id { get; set; }

    public int? TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public NonconformitySource Source { get; set; }
    public NonconformityStatus Status { get; set; } = NonconformityStatus.Open;

    [Required, StringLength(500), Display(Name = "Uygunsuzluk")]
    public string Description { get; set; } = "";

    [StringLength(2000), Display(Name = "Kök neden")]
    public string? RootCause { get; set; }

    [StringLength(2000), Display(Name = "Düzeltici faaliyet")]
    public string? CorrectiveAction { get; set; }

    public int? ResponsibleId { get; set; }
    public AppUser? Responsible { get; set; }

    public DateTime DetectedAt { get; set; } = AppTime.UtcNow;

    [Display(Name = "Termin")]
    public DateTime? DueAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    [Display(Name = "Etkinlik doğrulandı")]
    public bool? EffectivenessVerified { get; set; }

    public bool IsOverdue => Status != NonconformityStatus.Closed
                             && DueAt != null && AppTime.UtcNow > DueAt;
}

// Bilgi bankası ve dokümante edilmiş prosedür (md. 7.5)
public class KnowledgeArticle
{
    public int Id { get; set; }

    [Required, StringLength(200), Display(Name = "Başlık")]
    public string Title { get; set; } = "";

    [Required, Display(Name = "İçerik")]
    public string Body { get; set; } = "";

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public int AuthorId { get; set; }
    public AppUser? Author { get; set; }

    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; } = AppTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Bu makaleyle kaç talep çözüldü: makalenin gerçek faydası
    public int UsedCount { get; set; }
}

// Talebi takip eden (CC) kullanıcı: talebe yazılan yanıtlardan haberdar olur
public class TicketFollower
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public int UserId { get; set; }
    public AppUser? User { get; set; }

    public DateTime AddedAt { get; set; } = AppTime.UtcNow;
}

// Chatbot'un bilgi bankasında karşılık bulamadığı soru. Aynı soru tekrar
// sorulursa yeni satır açılmaz, sayaç artar; yönetim bu listeden hangi
// makalelerin eksik olduğunu görür.
public class ChatbotQuestion
{
    public int Id { get; set; }

    [Required, StringLength(500), Display(Name = "Soru")]
    public string Text { get; set; } = "";

    public int AskCount { get; set; } = 1;
    public DateTime FirstAskedAt { get; set; } = AppTime.UtcNow;
    public DateTime LastAskedAt { get; set; } = AppTime.UtcNow;

    // Eksik makale yazıldıysa / soru ele alındıysa
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? ArticleId { get; set; }
}
