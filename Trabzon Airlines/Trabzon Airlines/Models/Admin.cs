using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Trabzon_Airlines.Models
{
    // Admin modeli - Yönetici kullanıcılar için
    // Admin model - For administrator users
    public class Admin
    {
        [Key] // Birincil anahtar
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Otomatik artan ID
        public int Id { get; set; }

        [Required(ErrorMessage = "Kullanıcı adı zorunludur")]
        [StringLength(50)]
        [Display(Name = "Kullanıcı Adı")]
        [Index(IsUnique = true)] // Benzersiz kullanıcı adı
        public string Username { get; set; }

        [Required(ErrorMessage = "Şifre zorunludur")]
        [StringLength(255)] // Hash için yeterli uzunluk
        [Display(Name = "Şifre")]
        public string Password { get; set; } // Hash'lenmiş şifre

        [Required(ErrorMessage = "E-posta zorunludur")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz")]
        [StringLength(100)]
        [Display(Name = "E-posta")]
        [Index(IsUnique = true)] // Benzersiz e-posta
        public string Email { get; set; }

        [StringLength(100)]
        [Display(Name = "Ad Soyad")]
        public string? FullName { get; set; } // Tam ad

        [Display(Name = "Aktif Mi")]
        public bool IsActive { get; set; } // Admin aktif mi

        [Display(Name = "Süper Admin")]
        public bool IsSuperAdmin { get; set; } // Süper admin mi (tüm yetkiler)

        [Display(Name = "Son Giriş Tarihi")]
        public DateTime? LastLoginDate { get; set; } // Son giriş tarihi

        [Display(Name = "Oluşturulma Tarihi")]
        public DateTime CreatedAt { get; set; } // Oluşturulma tarihi

        [Display(Name = "Güncelleme Tarihi")]
        public DateTime? UpdatedAt { get; set; } // Güncelleme tarihi
    }
}

