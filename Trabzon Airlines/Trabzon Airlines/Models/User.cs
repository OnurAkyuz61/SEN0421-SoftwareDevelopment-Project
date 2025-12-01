using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Trabzon_Airlines.Models
{
    // Kullanıcı modeli - Üye olan kullanıcılar için
    // User model - For registered users
    public class User
    {
        [Key] // Birincil anahtar
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Otomatik artan ID
        public int Id { get; set; }

        [Required(ErrorMessage = "Ad alanı zorunludur")] // Zorunlu alan
        [StringLength(50)] // Maksimum 50 karakter
        [Display(Name = "Ad")] // Görünen isim
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Soyad alanı zorunludur")]
        [StringLength(50)]
        [Display(Name = "Soyad")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "E-posta alanı zorunludur")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz")]
        [StringLength(100)]
        [Display(Name = "E-posta")]
        [Index(IsUnique = true)] // Benzersiz e-posta
        public string Email { get; set; }

        [Required(ErrorMessage = "Şifre alanı zorunludur")]
        [StringLength(255)] // Hash için yeterli uzunluk
        [Display(Name = "Şifre")]
        public string Password { get; set; } // Hash'lenmiş şifre

        [Required(ErrorMessage = "Telefon alanı zorunludur")]
        [StringLength(20)]
        [Display(Name = "Telefon")]
        public string Phone { get; set; }

        [StringLength(11)] // TC Kimlik No: 11 karakter
        [Display(Name = "TC Kimlik No")]
        public string? TcNumber { get; set; } // Nullable - Yabancılar için

        [Display(Name = "TC Vatandaşı Değilim")]
        public bool NotTurkishCitizen { get; set; } // TC vatandaşı değilse true

        [Required(ErrorMessage = "Doğum tarihi zorunludur")]
        [DataType(DataType.Date)]
        [Display(Name = "Doğum Tarihi")]
        public DateTime BirthDate { get; set; }

        [Required(ErrorMessage = "Cinsiyet seçimi zorunludur")]
        [StringLength(10)]
        [Display(Name = "Cinsiyet")]
        public string Gender { get; set; } // Erkek, Kadın, Diğer

        [Display(Name = "Kampanya Bildirimleri")]
        public bool MarketingConsent { get; set; } // Pazarlama izni

        [Display(Name = "KVKK Onayı")]
        public bool KvkkConsent { get; set; } // KVKK onayı

        [Display(Name = "Kayıt Tarihi")]
        public DateTime CreatedAt { get; set; } // Kayıt tarihi

        [Display(Name = "Son Güncelleme")]
        public DateTime? UpdatedAt { get; set; } // Son güncelleme tarihi (nullable)

        [Display(Name = "Aktif Mi")]
        public bool IsActive { get; set; } // Hesap aktif mi

        // Google ile giriş için
        // For Google login
        [StringLength(255)]
        public string? GoogleId { get; set; } // Google kullanıcı ID

        // Apple ile giriş için
        // For Apple login
        [StringLength(255)]
        public string? AppleId { get; set; } // Apple kullanıcı ID

        // Navigasyon özellikleri - İlişkiler
        // Navigation properties - Relationships
        public virtual ICollection<PackageReservation> PackageReservations { get; set; } // Paket rezervasyonları
        public virtual ICollection<DealReservation> DealReservations { get; set; } // Fırsat rezervasyonları
    }
}

