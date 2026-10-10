using Microsoft.EntityFrameworkCore;
using ServiceDesk.Models;

namespace ServiceDesk.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Ticket 1 - 0..1 Feedback
        mb.Entity<Ticket>()
          .HasOne(t => t.Feedback)
          .WithOne(f => f.Ticket)
          .HasForeignKey<Feedback>(f => f.TicketId);

        mb.Entity<SlaPolicy>().HasIndex(s => s.Priority).IsUnique();

        // Başlangıç verisi (migration ile tabloya yazılır)
        mb.Entity<SlaPolicy>().HasData(
            new SlaPolicy { Id = 1, Priority = Priority.Critical, ResolutionHours = 4 },
            new SlaPolicy { Id = 2, Priority = Priority.High, ResolutionHours = 8 },
            new SlaPolicy { Id = 3, Priority = Priority.Medium, ResolutionHours = 24 },
            new SlaPolicy { Id = 4, Priority = Priority.Low, ResolutionHours = 72 });
    }
}
