using Microsoft.EntityFrameworkCore;
using ServiceDesk.Models;

namespace ServiceDesk.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

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

        // Ticket N - 0..1 AppUser (talep eden)
        mb.Entity<Ticket>()
          .HasOne(t => t.Requester)
          .WithMany()
          .HasForeignKey(t => t.RequesterId)
          .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<SlaPolicy>().HasIndex(s => s.Priority).IsUnique();
        mb.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        mb.Entity<AppUser>().HasIndex(u => u.PhoneNumber).IsUnique();
        mb.Entity<AppUser>().Ignore(u => u.FullName);

        // Başlangıç verisi (migration ile tabloya yazılır)
        mb.Entity<SlaPolicy>().HasData(
            new SlaPolicy { Id = 1, Priority = Priority.Critical, ResolutionHours = 4 },
            new SlaPolicy { Id = 2, Priority = Priority.High, ResolutionHours = 8 },
            new SlaPolicy { Id = 3, Priority = Priority.Medium, ResolutionHours = 24 },
            new SlaPolicy { Id = 4, Priority = Priority.Low, ResolutionHours = 72 });

        mb.Entity<AppUser>().HasData(
            new AppUser
            {
                Id = 1,
                FirstName = "Demo",
                LastName = "Yönetici",
                Email = "admin@servicedesk.local",
                Password = "admin123",
                PhoneNumber = "05321234567",
                Role = UserRole.Admin
            },
            new AppUser
            {
                Id = 2,
                FirstName = "Ayşe",
                LastName = "Destek",
                Email = "agent@servicedesk.local",
                Password = "agent123",
                PhoneNumber = "05329876543",
                Role = UserRole.Agent
            },
            new AppUser
            {
                Id = 3,
                FirstName = "Mehmet",
                LastName = "Kullanıcı",
                Email = "user@servicedesk.local",
                Password = "user123",
                PhoneNumber = "05321112233",
                Role = UserRole.User
            });
    }
}
