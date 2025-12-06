using System; // DateTime için
using System.ComponentModel.DataAnnotations; // Data annotations için

namespace Trabzon_Airlines.Models
{
    // Admin modeli - Yönetici kullanıcılar için
    // Admin model - For administrator users
    public class Admin
    {
        // Admin ID'si - Birincil anahtar
        // Admin ID - Primary key
        [Key] // Birincil anahtar attribute'u
        public int AdminId { get; set; }

        // Admin tam adı - Ad ve soyad
        // Admin full name - First and last name
        [Required(ErrorMessage = "Ad soyad zorunludur")] // Zorunlu alan
        [StringLength(100)] // Maksimum 100 karakter
        [Display(Name = "Ad Soyad")] // Görünen isim
        public string FullName { get; set; }

        // E-posta adresi - Benzersiz olmalı
        // Email address - Must be unique
        [Required(ErrorMessage = "E-posta zorunludur")] // Zorunlu alan
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz")] // E-posta formatı kontrolü
        [StringLength(100)] // Maksimum 100 karakter
        [Display(Name = "E-posta")] // Görünen isim
        public string Email { get; set; }

        // Şifre - Hash'lenmiş olarak saklanır
        // Password - Stored as hashed
        [Required(ErrorMessage = "Şifre zorunludur")] // Zorunlu alan
        [StringLength(255)] // Hash için yeterli uzunluk
        [Display(Name = "Şifre")] // Görünen isim
        public string Password { get; set; }
    }
}
