namespace Trabzon_Airlines.Migrations
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using Trabzon_Airlines.Models;

    internal sealed class Configuration : DbMigrationsConfiguration<Trabzon_Airlines.Data.TAContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true; // Development ortamı için
        }

        protected override void Seed(Trabzon_Airlines.Data.TAContext context)
        {
            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method
            //  to avoid creating duplicate seed data.

            // Varsayılan admin kullanıcısı oluştur (eğer yoksa)
            // Create default admin user (if not exists)
            if (!context.Admins.Any(a => a.Email == "admin@trabzonairlines.com"))
            {
                // SHA256 ile şifreyi hash'le - Hash password with SHA256
                string password = "admin123"; // Varsayılan şifre - Default password
                string hashedPassword = HashPassword(password);

                // Admin kullanıcısı oluştur - Create admin user
                context.Admins.AddOrUpdate(
                    a => a.Email,
                    new Admin
                    {
                        FullName = "Admin User",
                        Email = "admin@trabzonairlines.com",
                        Password = hashedPassword
                    }
                );

                // Değişiklikleri kaydet - Save changes
                context.SaveChanges();
            }
        }

        // SHA256 şifre hash'leme fonksiyonu - SHA256 password hashing function
        private string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // Şifreyi byte array'e çevir - Convert password to byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));

                // Byte array'i hexadecimal string'e çevir - Convert byte array to hexadecimal string
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2")); // Her byte'ı 2 haneli hex'e çevir - Convert each byte to 2-digit hex
                }

                // Hash'lenmiş şifreyi döndür - Return hashed password
                return builder.ToString();
            }
        }
    }
}
