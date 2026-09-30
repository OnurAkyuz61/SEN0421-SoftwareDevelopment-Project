using System; // DateTime için
using System.Collections.Generic; // List koleksiyonları için
using System.ComponentModel.DataAnnotations; // Data annotations için
using System.Linq; // LINQ sorguları için
using System.Web; // HTTP context için

namespace Trabzon_Airlines.Models
{
    // Koltuk modeli - Uçak koltukları için
    // Seat model - For aircraft seats
    public class Seat
    {
        // Koltuk ID'si - Birincil anahtar
        // Seat ID - Primary key
        [Key] // Birincil anahtar attribute'u
        public int SeatId { get; set; }

        // Koltuk numarası - Örn: 1A, 23B, 14C
        // Seat number - E.g.: 1A, 23B, 14C
        [Required(ErrorMessage = "Koltuk numarası zorunludur")] // Zorunlu alan
        [StringLength(10)] // Maksimum 10 karakter
        [Display(Name = "Koltuk Numarası")] // Görünen isim
        public string SeatNumber { get; set; } // Koltuk numarası 1A, 23B gibi

        // Kabin sınıfı - Business veya Economy
        // Cabin class - Business or Economy
        [Required(ErrorMessage = "Kabin sınıfı zorunludur")] // Zorunlu alan
        [StringLength(20)] // Maksimum 20 karakter
        [Display(Name = "Kabin Sınıfı")] // Görünen isim
        public string CabinClass { get; set; } // Business veya Ekonomi
    }
}
