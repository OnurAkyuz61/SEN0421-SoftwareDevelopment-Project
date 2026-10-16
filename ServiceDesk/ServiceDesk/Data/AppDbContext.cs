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

        mb.Entity<SlaPolicy>().HasIndex(s => s.Priority).IsUnique();
        mb.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        // Telefon opsiyonel: yalnızca dolu olanlar benzersiz olmalı
        mb.Entity<AppUser>().HasIndex(u => u.PhoneNumber).IsUnique()
          .HasFilter("[PhoneNumber] IS NOT NULL");
        mb.Entity<AppUser>().Ignore(u => u.FullName);

        // Başlangıç verisi (migration ile tabloya yazılır)
        mb.Entity<SlaPolicy>().HasData(
            new SlaPolicy { Id = 1, Priority = Priority.Critical, ResolutionHours = 4 },
            new SlaPolicy { Id = 2, Priority = Priority.High, ResolutionHours = 8 },
            new SlaPolicy { Id = 3, Priority = Priority.Medium, ResolutionHours = 24 },
            new SlaPolicy { Id = 4, Priority = Priority.Low, ResolutionHours = 72 });

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
