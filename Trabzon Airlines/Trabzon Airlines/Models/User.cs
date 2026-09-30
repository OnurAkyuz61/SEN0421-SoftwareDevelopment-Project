using System; // DateTime için
using System.ComponentModel.DataAnnotations; // Data annotations için

namespace Trabzon_Airlines.Models
{
    // Kullanıcı modeli - Üye olan kullanıcılar için
    // User model - For registered users
    public class User
    {
        // Kullanıcı ID'si - Birincil anahtar
        // User ID - Primary key
        [Key] // Birincil anahtar attribute'u
        public int UserId { get; set; }

        // Kullanıcı adı - İsim
        // User first name
        [Required(ErrorMessage = "Ad zorunludur")] // Zorunlu alan
        [StringLength(50)] // Maksimum 50 karakter
        [Display(Name = "Ad")] // Görünen isim
        public string FirstName { get; set; }

        // Kullanıcı soyadı
        // User last name
        [Required(ErrorMessage = "Soyad zorunludur")] // Zorunlu alan
        [StringLength(50)] // Maksimum 50 karakter
        [Display(Name = "Soyad")] // Görünen isim
        public string LastName { get; set; }

        // E-posta adresi - Benzersiz olmalı
        // Email address - Must be unique
        [Required(ErrorMessage = "E-posta zorunludur")] // Zorunlu alan
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz")] // E-posta formatı kontrolü
        [StringLength(100)] // Maksimum 100 karakter
        [Display(Name = "E-posta")] // Görünen isim
        public string Email { get; set; }

        // Telefon numarası - Opsiyonel
        // Phone number - Optional
        [StringLength(20)] // Maksimum 20 karakter
        [Display(Name = "Telefon")] // Görünen isim
        public string Phone { get; set; }

        // TC Kimlik Numarası - Opsiyonel (yabancılar için)
        // Turkish ID number - Optional (for foreigners)
        [StringLength(11)] // TC kimlik 11 karakter
        [Display(Name = "TC Kimlik No")] // Görünen isim
        public string TcNumber { get; set; }

        // Doğum tarihi - Zorunlu
        // Birth date - Required
        [Required(ErrorMessage = "Doğum tarihi zorunludur")] // Zorunlu alan
        [DataType(DataType.Date)] // Tarih tipi
        [Display(Name = "Doğum Tarihi")] // Görünen isim
        public DateTime BirthDate { get; set; }

        // Cinsiyet - Erkek, Kadın, Diğer
        // Gender - Male, Female, Other
        [StringLength(10)] // Maksimum 10 karakter
        [Display(Name = "Cinsiyet")] // Görünen isim
        public string Gender { get; set; }

        // Şifre - Hash'lenmiş olarak saklanır
        // Password - Stored as hashed
        [Required(ErrorMessage = "Şifre zorunludur")] // Zorunlu alan
        [StringLength(255)] // Hash için yeterli uzunluk
        [Display(Name = "Şifre")] // Görünen isim
        public string Password { get; set; }
    }
}
