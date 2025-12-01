using System.Data.Entity;

namespace Trabzon_Airlines.Models
{
    // Veritabanı bağlam sınıfı - Entity Framework DbContext
    // Database context class - Entity Framework DbContext
    public class TrabzonAirlinesDbContext : DbContext
    {
        // Veritabanı bağlantı adı - Web.config'de tanımlı
        // Database connection name - Defined in Web.config
        public TrabzonAirlinesDbContext() : base("TrabzonAirlinesDb")
        {
            // Veritabanı başlatma stratejisi
            // Database initialization strategy
            Database.SetInitializer(new CreateDatabaseIfNotExists<TrabzonAirlinesDbContext>());
        }

        // Kullanıcılar tablosu
        // Users table
        public DbSet<User> Users { get; set; }

        // Paketler tablosu
        // Packages table
        public DbSet<Package> Packages { get; set; }

        // Fırsatlar tablosu
        // Deals table
        public DbSet<Deal> Deals { get; set; }

        // Adminler tablosu
        // Admins table
        public DbSet<Admin> Admins { get; set; }

        // Paket rezervasyonları tablosu
        // Package reservations table
        public DbSet<PackageReservation> PackageReservations { get; set; }

        // Fırsat rezervasyonları tablosu
        // Deal reservations table
        public DbSet<DealReservation> DealReservations { get; set; }

        // Model yapılandırması - Fluent API ile özelleştirmeler
        // Model configuration - Customizations with Fluent API
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Kullanıcı tablosu yapılandırması
            // User table configuration
            modelBuilder.Entity<User>()
                .HasMany(u => u.PackageReservations)
                .WithRequired(r => r.User)
                .HasForeignKey(r => r.UserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<User>()
                .HasMany(u => u.DealReservations)
                .WithRequired(r => r.User)
                .HasForeignKey(r => r.UserId)
                .WillCascadeOnDelete(false);

            // Paket tablosu yapılandırması
            // Package table configuration
            modelBuilder.Entity<Package>()
                .HasMany(p => p.Reservations)
                .WithRequired(r => r.Package)
                .HasForeignKey(r => r.PackageId)
                .WillCascadeOnDelete(false);

            // Fırsat tablosu yapılandırması
            // Deal table configuration
            modelBuilder.Entity<Deal>()
                .HasMany(d => d.Reservations)
                .WithRequired(r => r.Deal)
                .HasForeignKey(r => r.DealId)
                .WillCascadeOnDelete(false);
        }
    }
}

