using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Data;

public class AppDbContext : DbContext
{
    // Geçmiş kaydına "kim değiştirdi" yazabilmek için oturuma erişilir.
    // CurrentUser servisi bu context'e bağımlı olduğundan oturum doğrudan okunur.
    private readonly IHttpContextAccessor? _http;

    public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor? http = null)
        : base(options) => _http = http;

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
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

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Ticket 1 - 0..1 Feedback
        mb.Entity<Ticket>()
          .HasOne(t => t.Feedback)
          .WithOne(f => f.Ticket)
          .HasForeignKey<Feedback>(f => f.TicketId);

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

        // Aynı kişi aynı adımda iki kez karar veremez
        mb.Entity<ChangeApproval>().HasIndex(a => new { a.TicketId, a.Step, a.ApproverId }).IsUnique();

        mb.Entity<Ticket>().HasQueryFilter(t => !t.IsDeleted);
        mb.Entity<ChangeDetail>().HasQueryFilter(d => !d.Ticket!.IsDeleted);
        mb.Entity<ChangeApproval>().HasQueryFilter(a => !a.Ticket!.IsDeleted);
        mb.Entity<Attachment>().HasQueryFilter(a => !a.Ticket!.IsDeleted);
        mb.Entity<Feedback>().HasQueryFilter(f => !f.Ticket!.IsDeleted);
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

        mb.Entity<EscalationRule>().HasData(
            new EscalationRule { Id = 1, Level = 1, AfterMinutes = 0, TargetRole = UserRole.Agent, IsActive = true },
            new EscalationRule { Id = 2, Level = 2, AfterMinutes = 120, TargetRole = UserRole.Admin, IsActive = true });

        // Demo kullanıcılar. Şifre özetleri PasswordHasher<AppUser> biçiminde SABİT
        // değerlerdir (sırasıyla admin123 / agent123 / user123). Hash her derlemede
        // yeniden üretilseydi tuz değişir, EF her seferinde "model değişti" deyip
        // boş migration üretirdi. Yeni şifre gerekirse özeti bir kez üretip buraya yapıştır.
        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        mb.Entity<AppUser>().HasData(
            new AppUser
            {
                Id = 1,
                FirstName = "Demo",
                LastName = "Yönetici",
                Email = "admin@servicedesk.local",
                PasswordHash = "AQAAAAIAAYagAAAAEFi3JF03e5L+rT0TFVRNIDpLX9U4gHjj97qMn+Eb/motK5gw/4voqGzIWdLDQnLoRg==",
                PhoneNumber = "05321234567",
                Role = UserRole.Admin,
                DepartmentId = 1,
                IsActive = true,
                CreatedAt = seedDate
            },
            new AppUser
            {
                Id = 2,
                FirstName = "Ayşe",
                LastName = "Destek",
                Email = "agent@servicedesk.local",
                PasswordHash = "AQAAAAIAAYagAAAAEGkvNowp+RHWmgqpxlh3o9uxa4AB+oXFA7R1IxRkUxU8Phrot1adAxfk+tsgti4wag==",
                PhoneNumber = "05329876543",
                Role = UserRole.Agent,
                DepartmentId = 1,
                TeamId = 1,
                IsActive = true,
                CreatedAt = seedDate
            },
            new AppUser
            {
                Id = 3,
                FirstName = "Mehmet",
                LastName = "Kullanıcı",
                Email = "user@servicedesk.local",
                PasswordHash = "AQAAAAIAAYagAAAAEGCuZpCsLC2ztoJiLtcHCbzkXXyQBZZ6US8lq80NpQ9uHcFc6ORCHZpysx1O4810DA==",
                PhoneNumber = "05321112233",
                Role = UserRole.User,
                DepartmentId = 2,
                IsActive = true,
                CreatedAt = seedDate
            });
    }

    // Talep üzerindeki alan değişiklikleri kaydedilmeden önce geçmişe yazılır.
    // Böylece controller'lara tek satır eklemeden her değişiklik iz bırakır.
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TicketHistories.AddRange(BuildHistory());
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        TicketHistories.AddRange(BuildHistory());
        return base.SaveChanges();
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
        }

        return logs;
    }

    private string UserName(int? id) =>
        id == null ? "Atanmadı" : Users.Find(id)?.FullName ?? $"#{id}";

    // Arka plan işlerinde ve oturumsuz isteklerde boş döner
    private int? CurrentUserId()
    {
        try { return _http?.HttpContext?.Session.GetInt32(CurrentUser.SessionKey); }
        catch (InvalidOperationException) { return null; }
    }
}
