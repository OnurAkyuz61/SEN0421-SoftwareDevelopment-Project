using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Data;

public class AppDbContext : DbContext
{
    // Geçmiş kaydına "kim değiştirdi" yazabilmek için oturuma erişilir.
    // CurrentUser servisi bu context'e bağımlı olduğundan oturum doğrudan okunur.
    private readonly IHttpContextAccessor? _http;

    // Okunmamış bildirim sayacı önbelleği: bildirim eklenince / okununca
    // ilgili kullanıcının sayacı burada, kayıttan hemen sonra geçersiz kılınır.
    // Böylece hangi yoldan (controller, arka plan servisi) yazılırsa yazılsın
    // önbellek bayat kalmaz.
    private readonly UnreadCountCache? _unreadCache;

    public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor? http = null,
                        UnreadCountCache? unreadCache = null)
        : base(options)
    {
        _http = http;
        _unreadCache = unreadCache;
    }

    public DbSet<OtpDeliveryLog> OtpDeliveryLogs => Set<OtpDeliveryLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<ApprovalDelegation> ApprovalDelegations => Set<ApprovalDelegation>();
    public DbSet<ApprovalChainRule> ApprovalChainRules => Set<ApprovalChainRule>();

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<SurveyTemplate> SurveyTemplates => Set<SurveyTemplate>();
    public DbSet<SurveyQuestion> SurveyQuestions => Set<SurveyQuestion>();
    public DbSet<SurveyResponse> SurveyResponses => Set<SurveyResponse>();
    public DbSet<SurveyAnswer> SurveyAnswers => Set<SurveyAnswer>();
    public DbSet<KpiSnapshot> KpiSnapshots => Set<KpiSnapshot>();
    public DbSet<Nonconformity> Nonconformities => Set<Nonconformity>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<KnowledgeArticleRevision> KnowledgeArticleRevisions => Set<KnowledgeArticleRevision>();
    public DbSet<KnowledgeArticleFeedback> KnowledgeArticleFeedbacks => Set<KnowledgeArticleFeedback>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();
    public DbSet<BusinessCalendar> BusinessCalendars => Set<BusinessCalendar>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();
    public DbSet<EscalationRecord> EscalationRecords => Set<EscalationRecord>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ChangeDetail> ChangeDetails => Set<ChangeDetail>();
    public DbSet<ChangeApproval> ChangeApprovals => Set<ChangeApproval>();
    public DbSet<TicketFollower> TicketFollowers => Set<TicketFollower>();
    public DbSet<ChatbotQuestion> ChatbotQuestions => Set<ChatbotQuestion>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── Takipçi (CC), birim takvimi, SLA eşiği / sürümü ─────────────
        mb.Entity<TicketFollower>()
          .HasOne(f => f.Ticket).WithMany(t => t.Followers)
          .HasForeignKey(f => f.TicketId)
          .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<TicketFollower>()
          .HasOne(f => f.User).WithMany()
          .HasForeignKey(f => f.UserId)
          .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<TicketFollower>().HasIndex(f => new { f.TicketId, f.UserId }).IsUnique();
        mb.Entity<TicketFollower>().HasQueryFilter(f => !f.Ticket!.IsDeleted);

        mb.Entity<Department>()
          .HasOne(d => d.BusinessCalendar).WithMany()
          .HasForeignKey(d => d.BusinessCalendarId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<SlaPolicy>().Property(p => p.WarningMinutesBefore).HasDefaultValue(120);
        mb.Entity<SlaPolicy>().Property(p => p.Version).HasDefaultValue(1);
        mb.Entity<KpiSnapshot>().Property(k => k.FirstResponseComplianceRate).HasPrecision(5, 1);
        mb.Entity<Ticket>().Ignore(t => t.TagList);

        // Ticket 1 - 0..1 SurveyResponse
        mb.Entity<SurveyResponse>()
          .HasOne(r => r.Ticket).WithOne(t => t.Survey)
          .HasForeignKey<SurveyResponse>(r => r.TicketId)
          .OnDelete(DeleteBehavior.Cascade);

        // Ticket 1 - N Attachment (talep silinirse ekleri de silinir)
        mb.Entity<Attachment>()
          .HasOne(a => a.Ticket)
          .WithMany(t => t.Attachments)
          .HasForeignKey(a => a.TicketId)
          .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Attachment>().Ignore(a => a.IsImage);

        // Attachment N - 0..1 AppUser (yükleyen)
        mb.Entity<Attachment>()
          .HasOne(a => a.UploadedBy)
          .WithMany()
          .HasForeignKey(a => a.UploadedById)
          .OnDelete(DeleteBehavior.Restrict);

        // Attachment N - 0..1 AppUser (eki kaldıran). Denetim izi olduğu için
        // kullanıcı silinemez; ayrılan personel IsActive = false ile pasifleşir.
        mb.Entity<Attachment>()
          .HasOne(a => a.DeletedBy)
          .WithMany()
          .HasForeignKey(a => a.DeletedById)
          .OnDelete(DeleteBehavior.Restrict);

        // Ticket 1 - N TicketComment (talep silinirse yazışma da silinir)
        mb.Entity<TicketComment>()
          .HasOne(c => c.Ticket)
          .WithMany(t => t.Comments)
          .HasForeignKey(c => c.TicketId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<TicketComment>()
          .HasOne(c => c.Author)
          .WithMany()
          .HasForeignKey(c => c.AuthorId)
          .OnDelete(DeleteBehavior.Restrict);

        // Mesaja eklenen dosya. Talep silindiğinde ekler zaten Ticket üzerinden
        // cascade ile gidiyor; bu bağ da cascade olsaydı SQL Server çoklu silme yolu
        // hatası verirdi. NoAction aynı zamanda eki olan bir mesajın tek başına
        // silinmesini de engeller — yazışma kaydı denetim izinin parçasıdır.
        mb.Entity<Attachment>()
          .HasOne(a => a.Comment)
          .WithMany(c => c.Attachments)
          .HasForeignKey(a => a.CommentId)
          .OnDelete(DeleteBehavior.NoAction);

        // Ticket 1 - N TicketHistory
        mb.Entity<TicketHistory>()
          .HasOne(h => h.Ticket)
          .WithMany(t => t.History)
          .HasForeignKey(h => h.TicketId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<TicketHistory>()
          .HasOne(h => h.ChangedBy)
          .WithMany()
          .HasForeignKey(h => h.ChangedById)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<TicketComment>().HasIndex(c => new { c.TicketId, c.CreatedAt });
        mb.Entity<TicketHistory>().HasIndex(h => new { h.TicketId, h.ChangedAt });

        // Talep N - 0..1 SlaPolicy: uygulanan taahhüt sabitlenir, politika silinemez
        mb.Entity<Ticket>()
          .HasOne(t => t.SlaPolicy)
          .WithMany()
          .HasForeignKey(t => t.SlaPolicyId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<SlaPolicy>()
          .HasOne(s => s.BusinessCalendar)
          .WithMany()
          .HasForeignKey(s => s.BusinessCalendarId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Holiday>()
          .HasOne(h => h.BusinessCalendar)
          .WithMany(c => c.Holidays)
          .HasForeignKey(h => h.BusinessCalendarId)
          .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Holiday>().HasIndex(h => new { h.BusinessCalendarId, h.Date }).IsUnique();

        mb.Entity<EscalationRecord>()
          .HasOne(e => e.Ticket)
          .WithMany(t => t.Escalations)
          .HasForeignKey(e => e.TicketId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<EscalationRecord>()
          .HasOne(e => e.EscalatedTo)
          .WithMany()
          .HasForeignKey(e => e.EscalatedToId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<EscalationRule>().HasIndex(r => r.Level).IsUnique();

        mb.Entity<Notification>()
          .HasOne(n => n.Recipient)
          .WithMany()
          .HasForeignKey(n => n.RecipientId)
          .OnDelete(DeleteBehavior.Cascade);

        // Talep silinirse bildirim de gider; iki cascade yolu çakışmasın diye
        // talep bağı NoAction (bildirim talebe zorunlu bağlı değil)
        mb.Entity<Notification>()
          .HasOne(n => n.Ticket)
          .WithMany()
          .HasForeignKey(n => n.TicketId)
          .OnDelete(DeleteBehavior.NoAction);

        // Zil ve bildirim listesi bu sıralamayla okunuyor
        mb.Entity<Notification>().HasIndex(n => new { n.RecipientId, n.ReadAt, n.CreatedAt });

        // ── Katalog ve organizasyon ──────────────────────────────────────
        // Referans tablolar silinmez, IsActive = false ile pasifleştirilir;
        // bu yüzden bağların tamamı Restrict.
        mb.Entity<Team>()
          .HasOne(t => t.Department).WithMany(d => d.Teams)
          .HasForeignKey(t => t.DepartmentId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Team>()
          .HasOne(t => t.LeadUser).WithMany()
          .HasForeignKey(t => t.LeadUserId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Category>()
          .HasOne(c => c.Parent).WithMany(c => c.Children)
          .HasForeignKey(c => c.ParentId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Category>()
          .HasOne(c => c.DefaultTeam).WithMany()
          .HasForeignKey(c => c.DefaultTeamId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<AppUser>()
          .HasOne(u => u.Department).WithMany()
          .HasForeignKey(u => u.DepartmentId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<AppUser>()
          .HasOne(u => u.Team).WithMany()
          .HasForeignKey(u => u.TeamId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Ticket>()
          .HasOne(t => t.Category).WithMany()
          .HasForeignKey(t => t.CategoryId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Ticket>()
          .HasOne(t => t.AssignedTeam).WithMany()
          .HasForeignKey(t => t.AssignedTeamId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Ticket>().HasIndex(t => t.CategoryId);
        mb.Entity<Category>().HasIndex(c => new { c.IsActive, c.SortOrder });

        // Katalog isimleri benzersiz (pasif kayıtlar dahil). Asıl kontrol
        // CatalogController'da anlaşılır mesajla yapılır; indeks aynı anda
        // gelen iki kaydın ikisinin de geçmesini engeller.
        mb.Entity<Category>().HasIndex(c => c.Name).IsUnique();
        mb.Entity<Department>().HasIndex(d => d.Name).IsUnique();
        mb.Entity<Team>().HasIndex(t => t.Name).IsUnique();

        // ── Talep numarası ve yumuşak silme ──────────────────────────────
        mb.Entity<Ticket>().HasIndex(t => t.Number).IsUnique()
          .HasFilter("[Number] IS NOT NULL");

        mb.Entity<Ticket>()
          .HasOne(t => t.DeletedBy).WithMany()
          .HasForeignKey(t => t.DeletedById)
          .OnDelete(DeleteBehavior.Restrict);

        // Silinmiş talep hiçbir sorguya düşmez. Bağımlı tabloların da aynı
        // filtreyi taşıması gerekiyor; aksi halde zorunlu ilişki ile filtre
        // çakışır ve EF uyarı verir.
        // ── Değişiklik yönetimi ──────────────────────────────────────────
        // Plan talebe bire bir bağlı; birincil anahtar talebin kendisi
        mb.Entity<ChangeDetail>().HasKey(d => d.TicketId);
        mb.Entity<ChangeDetail>()
          .HasOne(d => d.Ticket).WithOne(t => t.ChangeDetail)
          .HasForeignKey<ChangeDetail>(d => d.TicketId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<ChangeDetail>()
          .HasOne(d => d.UpdatedBy).WithMany()
          .HasForeignKey(d => d.UpdatedById)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ChangeApproval>()
          .HasOne(a => a.Ticket).WithMany(t => t.Approvals)
          .HasForeignKey(a => a.TicketId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<ChangeApproval>()
          .HasOne(a => a.Approver).WithMany()
          .HasForeignKey(a => a.ApproverId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ChangeApproval>()
          .HasOne(a => a.OnBehalfOf).WithMany()
          .HasForeignKey(a => a.OnBehalfOfId)
          .OnDelete(DeleteBehavior.Restrict);

        // Aynı kişi aynı turda aynı adımda iki kez karar veremez
        // (yeniden gönderilen talepte yeni tur açılır)
        mb.Entity<ChangeApproval>().HasIndex(a => new { a.TicketId, a.Round, a.Step, a.ApproverId }).IsUnique();
        mb.Entity<ChangeApproval>().Property(a => a.Round).HasDefaultValue(1);
        mb.Entity<Ticket>().Property(t => t.ApprovalRound).HasDefaultValue(1);
        mb.Entity<Ticket>().Ignore(t => t.CurrentRoundApprovals);
        mb.Entity<Ticket>().Ignore(t => t.DefaultApprovalSteps);

        // ── Mesajlaşma, denetim, tercih ──────────────────────────────────
        mb.Entity<OtpDeliveryLog>()
          .HasOne(o => o.User).WithMany()
          .HasForeignKey(o => o.UserId)
          .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<OtpDeliveryLog>().HasIndex(o => new { o.UserId, o.CreatedAt });
        mb.Entity<OtpDeliveryLog>().HasIndex(o => o.ProviderMessageId);

        mb.Entity<AuditLog>()
          .HasOne(a => a.User).WithMany()
          .HasForeignKey(a => a.UserId)
          .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<AuditLog>().HasIndex(a => new { a.Action, a.CreatedAt });
        mb.Entity<AuditLog>().HasIndex(a => new { a.UserId, a.CreatedAt });

        mb.Entity<NotificationPreference>()
          .HasOne(p => p.User).WithMany()
          .HasForeignKey(p => p.UserId)
          .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<NotificationPreference>().HasIndex(p => new { p.UserId, p.Type }).IsUnique();

        // Kuyruk worker'ı bu sırayla okur: gönderilmemiş, başarısız sayılmamış, zamanı gelmiş
        mb.Entity<Notification>().HasIndex(n => new { n.Channel, n.IsSent, n.IsFailed, n.NextAttemptAt });

        mb.Entity<ApprovalDelegation>()
          .HasOne(d => d.Delegator).WithMany()
          .HasForeignKey(d => d.DelegatorId)
          .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ApprovalDelegation>()
          .HasOne(d => d.Delegate).WithMany()
          .HasForeignKey(d => d.DelegateId)
          .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ApprovalDelegation>().HasIndex(d => new { d.DelegateId, d.StartsAt, d.EndsAt });

        mb.Entity<ApprovalChainRule>()
          .HasOne(r => r.Category).WithMany()
          .HasForeignKey(r => r.CategoryId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<EscalationRule>()
          .HasOne(r => r.FallbackUser).WithMany()
          .HasForeignKey(r => r.FallbackUserId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<EscalationRecord>()
          .HasOne(e => e.TriggeredBy).WithMany()
          .HasForeignKey(e => e.TriggeredById)
          .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<EscalationRecord>().HasIndex(e => e.OccurredAt);

        mb.Entity<AppUser>().Property(u => u.DailyDigestEnabled).HasDefaultValue(true);
        mb.Entity<EscalationRule>().Property(r => r.RaisePriority).HasDefaultValue(true);

        // ── ISO kalite katmanı ───────────────────────────────────────────
        mb.Entity<SurveyQuestion>()
          .HasOne(q => q.Template).WithMany(t => t.Questions)
          .HasForeignKey(q => q.TemplateId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<SurveyResponse>()
          .HasOne(r => r.Template).WithMany()
          .HasForeignKey(r => r.TemplateId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<SurveyResponse>()
          .HasOne(r => r.SubmittedBy).WithMany()
          .HasForeignKey(r => r.SubmittedById)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<SurveyAnswer>()
          .HasOne(a => a.Response).WithMany(r => r.Answers)
          .HasForeignKey(a => a.ResponseId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<SurveyAnswer>()
          .HasOne(a => a.Question).WithMany()
          .HasForeignKey(a => a.QuestionId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<SurveyAnswer>().HasIndex(a => new { a.ResponseId, a.QuestionId }).IsUnique();
        mb.Entity<SurveyResponse>().Ignore(r => r.Average);

        // Aynı dönem ve birim için tek anlık görüntü
        mb.Entity<KpiSnapshot>()
          .HasOne(k => k.Department).WithMany()
          .HasForeignKey(k => k.DepartmentId)
          .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<KpiSnapshot>()
          .HasIndex(k => new { k.Period, k.PeriodStart, k.DepartmentId }).IsUnique();

        // Oran ve ortalamalar: varsayılan hassasiyette sessizce kırpılmasın
        mb.Entity<KpiSnapshot>().Property(k => k.SlaComplianceRate).HasPrecision(5, 1);
        mb.Entity<KpiSnapshot>().Property(k => k.AvgResolutionHours).HasPrecision(9, 1);
        mb.Entity<KpiSnapshot>().Property(k => k.AvgFirstResponseHours).HasPrecision(9, 1);
        mb.Entity<KpiSnapshot>().Property(k => k.CsatAverage).HasPrecision(3, 2);
        mb.Entity<KpiSnapshot>().Property(k => k.CsatResponseRate).HasPrecision(5, 1);

        mb.Entity<Nonconformity>()
          .HasOne(n => n.Ticket).WithMany()
          .HasForeignKey(n => n.TicketId)
          .OnDelete(DeleteBehavior.NoAction);
        mb.Entity<Nonconformity>()
          .HasOne(n => n.Responsible).WithMany()
          .HasForeignKey(n => n.ResponsibleId)
          .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Nonconformity>().HasIndex(n => new { n.Status, n.DueAt });
        mb.Entity<Nonconformity>().Ignore(n => n.IsOverdue);

        mb.Entity<KnowledgeArticle>()
          .HasOne(a => a.Category).WithMany()
          .HasForeignKey(a => a.CategoryId)
          .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<KnowledgeArticle>()
          .HasOne(a => a.Author).WithMany()
          .HasForeignKey(a => a.AuthorId)
          .OnDelete(DeleteBehavior.Restrict);
        // Onaya gönderen / onaylayan: kullanıcı pasifleşse de kayıt durur
        mb.Entity<KnowledgeArticle>()
          .HasOne(a => a.SubmittedBy).WithMany()
          .HasForeignKey(a => a.SubmittedById)
          .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<KnowledgeArticle>()
          .HasOne(a => a.ApprovedBy).WithMany()
          .HasForeignKey(a => a.ApprovedById)
          .OnDelete(DeleteBehavior.Restrict);
        // Yayın kuyruğu ve listeleme bu kolonun üzerinden geçer
        mb.Entity<KnowledgeArticle>().HasIndex(a => a.Status);

        // Sürüm geçmişi: makale silinirse geçmişi de gider (makalesiz sürüm
        // kaydının anlamı yok), yazan kullanıcı ise korunur.
        mb.Entity<KnowledgeArticleRevision>()
          .HasOne(r => r.Article).WithMany(a => a.Revisions)
          .HasForeignKey(r => r.ArticleId)
          .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<KnowledgeArticleRevision>()
          .HasOne(r => r.CreatedBy).WithMany()
          .HasForeignKey(r => r.CreatedById)
          .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<KnowledgeArticleRevision>()
          .HasOne(r => r.Category).WithMany()
          .HasForeignKey(r => r.CategoryId)
          .OnDelete(DeleteBehavior.Restrict);
        // Sürüm numarası makale içinde tekrar etmez: eşzamanlı iki kaydetme
        // aynı numarayı üretmeye çalışırsa veritabanı reddeder.
        mb.Entity<KnowledgeArticleRevision>()
          .HasIndex(r => new { r.ArticleId, r.VersionNo }).IsUnique();

        // Okuyucu oyu: makale silinirse oyları da gider, kullanıcı korunur
        mb.Entity<KnowledgeArticleFeedback>()
          .HasOne(f => f.Article).WithMany(a => a.Feedbacks)
          .HasForeignKey(f => f.ArticleId)
          .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<KnowledgeArticleFeedback>()
          .HasOne(f => f.User).WithMany()
          .HasForeignKey(f => f.UserId)
          .OnDelete(DeleteBehavior.Restrict);
        // Kullanıcı başına tek oy: kural uygulama katmanında DEĞİL burada,
        // veritabanında. İki istek aynı anda gelse bile ikincisi reddedilir
        // (controller bu ihlali "zaten oy verilmiş" olarak ele alır).
        mb.Entity<KnowledgeArticleFeedback>()
          .HasIndex(f => new { f.ArticleId, f.UserId }).IsUnique();

        mb.Entity<Ticket>()
          .HasOne(t => t.ResolvedByArticle).WithMany()
          .HasForeignKey(t => t.ResolvedByArticleId)
          .OnDelete(DeleteBehavior.Restrict);

        // Giriş denemeleri: kullanıcı silinmese de kayıt bağımsız durur
        mb.Entity<LoginAttempt>()
          .HasOne(l => l.User).WithMany()
          .HasForeignKey(l => l.UserId)
          .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<LoginAttempt>().HasIndex(l => new { l.Email, l.AttemptedAt });
        mb.Entity<LoginAttempt>().Ignore(l => l.IsSuccess);

        // Şifre sıfırlama: kullanıcı silinirse token'lar da gider; özet benzersiz
        mb.Entity<PasswordResetToken>()
          .HasOne(t => t.User).WithMany()
          .HasForeignKey(t => t.UserId)
          .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<PasswordResetToken>().HasIndex(t => t.TokenHash).IsUnique();
        mb.Entity<PasswordResetToken>().Ignore(t => t.IsUsable);

        mb.Entity<Ticket>().HasQueryFilter(t => !t.IsDeleted);
        mb.Entity<ChangeDetail>().HasQueryFilter(d => !d.Ticket!.IsDeleted);
        mb.Entity<ChangeApproval>().HasQueryFilter(a => !a.Ticket!.IsDeleted);
        // Ekin kendi yumuşak silmesi de burada: kaldırılan dosya hiçbir
        // listeye, sayıma ve indirmeye düşmez (AttachmentsController.Get
        // IgnoreQueryFilters kullandığı için koşulu orada elle yazıyor).
        mb.Entity<Attachment>().HasQueryFilter(a => !a.IsDeleted && !a.Ticket!.IsDeleted);
        mb.Entity<SurveyResponse>().HasQueryFilter(r => !r.Ticket!.IsDeleted);
        mb.Entity<SurveyAnswer>().HasQueryFilter(a => !a.Response!.Ticket!.IsDeleted);
        mb.Entity<TicketComment>().HasQueryFilter(c => !c.Ticket!.IsDeleted);
        mb.Entity<TicketHistory>().HasQueryFilter(h => !h.Ticket!.IsDeleted);
        mb.Entity<EscalationRecord>().HasQueryFilter(e => !e.Ticket!.IsDeleted);

        // Ticket N - 1 AppUser (talep eden). Talebi olan kullanıcı silinemez;
        // ayrılan personel IsActive = false ile pasifleştirilir.
        mb.Entity<Ticket>()
          .HasOne(t => t.Requester)
          .WithMany()
          .HasForeignKey(t => t.RequesterId)
          .OnDelete(DeleteBehavior.Restrict);

        // Ticket N - 0..1 AppUser (atanan destek personeli)
        mb.Entity<Ticket>()
          .HasOne(t => t.Assignee)
          .WithMany()
          .HasForeignKey(t => t.AssigneeId)
          .OnDelete(DeleteBehavior.Restrict);

        // Listeleme ve SLA taramalarının üzerinden geçtiği kolonlar
        mb.Entity<Ticket>().HasIndex(t => t.AssigneeId);
        mb.Entity<Ticket>().HasIndex(t => t.Status);
        mb.Entity<Ticket>().HasIndex(t => new { t.Status, t.SlaDueAt });   // süresi geçen açık talepler
        mb.Entity<Ticket>().HasIndex(t => t.ApprovalStatus);
        mb.Entity<Ticket>().HasIndex(t => t.CreatedAt);

        // Artık önceliğe birden fazla politika bağlanabilir (tür bazlı, sürümlü),
        // bu yüzden benzersiz değil
        mb.Entity<SlaPolicy>().HasIndex(s => new { s.Priority, s.IsActive });
        mb.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        // Telefon opsiyonel: yalnızca dolu olanlar benzersiz olmalı
        mb.Entity<AppUser>().HasIndex(u => u.PhoneNumber).IsUnique()
          .HasFilter("[PhoneNumber] IS NOT NULL");
        mb.Entity<AppUser>().Ignore(u => u.FullName);

        // ── Başlangıç verisi (migration ile tabloya yazılır) ─────────────
        var policyFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        mb.Entity<BusinessCalendar>().HasData(
            new BusinessCalendar
            {
                Id = 1,
                Name = "Varsayılan Mesai",
                WorkStartMinutes = 9 * 60,
                WorkEndMinutes = 18 * 60,
                WorkDays = "1,2,3,4,5"      // Pazartesi–Cuma
            });

        // Sabit tarihli resmî tatiller (dinî bayramlar her yıl kaydığı için
        // yönetim ekranından elle eklenecek)
        mb.Entity<Holiday>().HasData(
            new Holiday { Id = 1, BusinessCalendarId = 1, Date = new DateTime(2026, 1, 1), Name = "Yılbaşı" },
            new Holiday { Id = 2, BusinessCalendarId = 1, Date = new DateTime(2026, 4, 23), Name = "Ulusal Egemenlik ve Çocuk Bayramı" },
            new Holiday { Id = 3, BusinessCalendarId = 1, Date = new DateTime(2026, 5, 1), Name = "Emek ve Dayanışma Günü" },
            new Holiday { Id = 4, BusinessCalendarId = 1, Date = new DateTime(2026, 5, 19), Name = "Atatürk'ü Anma, Gençlik ve Spor Bayramı" },
            new Holiday { Id = 5, BusinessCalendarId = 1, Date = new DateTime(2026, 7, 15), Name = "Demokrasi ve Millî Birlik Günü" },
            new Holiday { Id = 6, BusinessCalendarId = 1, Date = new DateTime(2026, 8, 30), Name = "Zafer Bayramı" },
            new Holiday { Id = 7, BusinessCalendarId = 1, Date = new DateTime(2026, 10, 29), Name = "Cumhuriyet Bayramı" });

        // Kritik talepler 7x24 işler; diğerleri mesai saatleriyle sayılır
        mb.Entity<SlaPolicy>().HasData(
            new SlaPolicy
            {
                Id = 1, Name = "Kritik", Priority = Priority.Critical,
                FirstResponseMinutes = 30, ResolutionMinutes = 4 * 60,
                BusinessHoursOnly = false, BusinessCalendarId = null,
                EffectiveFrom = policyFrom, IsActive = true
            },
            new SlaPolicy
            {
                Id = 2, Name = "Yüksek", Priority = Priority.High,
                FirstResponseMinutes = 60, ResolutionMinutes = 8 * 60,
                BusinessHoursOnly = true, BusinessCalendarId = 1,
                EffectiveFrom = policyFrom, IsActive = true
            },
            new SlaPolicy
            {
                Id = 3, Name = "Orta", Priority = Priority.Medium,
                FirstResponseMinutes = 4 * 60, ResolutionMinutes = 24 * 60,
                BusinessHoursOnly = true, BusinessCalendarId = 1,
                EffectiveFrom = policyFrom, IsActive = true
            },
            new SlaPolicy
            {
                Id = 4, Name = "Düşük", Priority = Priority.Low,
                FirstResponseMinutes = 8 * 60, ResolutionMinutes = 72 * 60,
                BusinessHoursOnly = true, BusinessCalendarId = 1,
                EffectiveFrom = policyFrom, IsActive = true
            });

        mb.Entity<Department>().HasData(
            new Department { Id = 1, Name = "Bilgi İşlem", IsActive = true },
            new Department { Id = 2, Name = "İnsan Kaynakları", IsActive = true },
            new Department { Id = 3, Name = "Muhasebe", IsActive = true },
            new Department { Id = 4, Name = "Satış", IsActive = true });

        mb.Entity<Team>().HasData(
            new Team { Id = 1, Name = "Son Kullanıcı Desteği", DepartmentId = 1, IsActive = true },
            new Team { Id = 2, Name = "Ağ ve Sistem", DepartmentId = 1, IsActive = true });

        // Id'ler eski TicketCategory enum değerlerinin bir fazlası olacak şekilde
        // seçildi; migration içindeki taşıma bu eşlemeye dayanıyor
        mb.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Diğer", SortOrder = 99, IsActive = true },
            new Category { Id = 2, Name = "Donanım", DefaultTeamId = 1, SortOrder = 10, IsActive = true },
            new Category { Id = 3, Name = "Yazılım", DefaultTeamId = 1, SortOrder = 20, IsActive = true },
            new Category { Id = 4, Name = "Ağ / İnternet", DefaultTeamId = 2, SortOrder = 30, IsActive = true },
            new Category { Id = 5, Name = "Hesap / Erişim", DefaultTeamId = 2, SortOrder = 40, IsActive = true },
            new Category { Id = 6, Name = "E-posta", DefaultTeamId = 1, SortOrder = 50, IsActive = true });

        // Memnuniyet anketi: üç boyut ayrı ayrı ölçülür ki hangi tarafta
        // iyileştirme gerektiği görülebilsin
        mb.Entity<SurveyTemplate>().HasData(
            new SurveyTemplate { Id = 1, Name = "Destek Memnuniyet Anketi", IsActive = true });

        mb.Entity<SurveyQuestion>().HasData(
            new SurveyQuestion { Id = 1, TemplateId = 1, Text = "Çözüm hızından memnun kaldınız mı?", SortOrder = 10, IsActive = true },
            new SurveyQuestion { Id = 2, TemplateId = 1, Text = "Sorununuz kalıcı olarak çözüldü mü?", SortOrder = 20, IsActive = true },
            new SurveyQuestion { Id = 3, TemplateId = 1, Text = "Destek ekibinin ilgisinden memnun kaldınız mı?", SortOrder = 30, IsActive = true });

        mb.Entity<EscalationRule>().HasData(
            new EscalationRule { Id = 1, Level = 1, AfterMinutes = 0, TargetRole = UserRole.Agent, IsActive = true },
            new EscalationRule { Id = 2, Level = 2, AfterMinutes = 120, TargetRole = UserRole.Admin, IsActive = true });

        // NOT: Demo kullanıcılar burada DEĞİL. Referans veri (politika, kategori,
        // takvim, anket şablonu) her ortamda gerekli olduğu için HasData ile gelir;
        // test hesapları yalnızca Development ortamında DevDataSeeder tarafından
        // oluşturulur. Böylece canlı veritabanına bilinen şifreli hesap yazılmaz.
    }

    // Talep üzerindeki alan değişiklikleri kaydedilmeden önce geçmişe yazılır.
    // Böylece controller'lara tek satır eklemeden her değişiklik iz bırakır.
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TicketHistories.AddRange(BuildHistory());
        var touched = NotificationRecipientsTouched();
        var result = await base.SaveChangesAsync(cancellationToken);
        InvalidateUnread(touched);
        return result;
    }

    public override int SaveChanges()
    {
        TicketHistories.AddRange(BuildHistory());
        var touched = NotificationRecipientsTouched();
        var result = base.SaveChanges();
        InvalidateUnread(touched);
        return result;
    }

    // Uygulama içi bildirimi eklenen / okunan / silinen kullanıcılar
    private List<int> NotificationRecipientsTouched() =>
        _unreadCache == null
            ? new List<int>()
            : ChangeTracker.Entries<Notification>()
                .Where(e => e.Entity.Channel == NotificationChannel.InApp
                            && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => e.Entity.RecipientId)
                .Distinct()
                .ToList();

    private void InvalidateUnread(List<int> userIds)
    {
        foreach (var id in userIds) _unreadCache?.Invalidate(id);
    }

    private List<TicketHistory> BuildHistory()
    {
        var changedById = CurrentUserId();
        var now = AppTime.UtcNow;
        var logs = new List<TicketHistory>();

        foreach (var entry in ChangeTracker.Entries<Ticket>().Where(e => e.State == EntityState.Modified))
        {
            void Add(string field, string? oldValue, string? newValue)
            {
                if (oldValue == newValue) return;
                logs.Add(new TicketHistory
                {
                    TicketId = entry.Entity.Id,
                    Field = field,
                    OldValue = oldValue,
                    NewValue = newValue,
                    ChangedById = changedById,
                    ChangedAt = now
                });
            }

            var status = entry.Property(t => t.Status);
            if (status.IsModified)
                Add("Durum", Ui.StatusText(status.OriginalValue), Ui.StatusText(status.CurrentValue));

            var priority = entry.Property(t => t.Priority);
            if (priority.IsModified)
                Add("Öncelik", Ui.PriorityText(priority.OriginalValue), Ui.PriorityText(priority.CurrentValue));

            var approval = entry.Property(t => t.ApprovalStatus);
            if (approval.IsModified)
                Add("Onay", Ui.ApprovalText(approval.OriginalValue), Ui.ApprovalText(approval.CurrentValue));

            var assignee = entry.Property(t => t.AssigneeId);
            if (assignee.IsModified)
                Add("Atanan", UserName(assignee.OriginalValue), UserName(assignee.CurrentValue));

            var code = entry.Property(t => t.ResolutionCode);
            if (code.IsModified)
                Add("Çözüm kodu", Ui.ResolutionText(code.OriginalValue), Ui.ResolutionText(code.CurrentValue));

            var tags = entry.Property(t => t.Tags);
            if (tags.IsModified)
                Add("Etiketler", Cut(tags.OriginalValue), Cut(tags.CurrentValue));
        }

        return logs;
    }

    // SQL Server benzersiz indeks / anahtar ihlali (2601, 2627)
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 };

    // Geçmiş kolonları 100 karakter
    private static string? Cut(string? s) => s == null || s.Length <= 100 ? s : s[..100];

    private string UserName(int? id) =>
        id == null ? "Atanmadı" : Users.Find(id)?.FullName ?? $"#{id}";

    // Arka plan işlerinde ve oturumsuz isteklerde boş döner
    private int? CurrentUserId()
    {
        try { return _http?.HttpContext?.Session.GetInt32(CurrentUser.SessionKey); }
        catch (InvalidOperationException) { return null; }
    }
}
