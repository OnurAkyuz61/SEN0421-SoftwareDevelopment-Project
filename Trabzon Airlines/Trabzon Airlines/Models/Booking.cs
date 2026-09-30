using System; // DateTime için
using System.Collections.Generic; // List koleksiyonları için
using System.ComponentModel.DataAnnotations; // Data annotations için
using System.ComponentModel.DataAnnotations.Schema; // Database schema için
using System.Linq; // LINQ sorguları için
using System.Web; // HTTP context için

namespace Trabzon_Airlines.Models
{
    // Rezervasyon modeli - Uçuş rezervasyonları için
    // Booking model - For flight reservations
    public class Booking
    {
        // Rezervasyon ID'si - Birincil anahtar
        // Booking ID - Primary key
        [Key] // Birincil anahtar attribute'u
        public int BookingId { get; set; }

        // Uçuş ID'si - Hangi uçuş için rezervasyon
        // Flight ID - Which flight the reservation is for
        [Required(ErrorMessage = "Uçuş ID'si zorunludur")] // Zorunlu alan
        public int FlightId { get; set; }

        // Koltuk ID'si - Hangi koltuk için rezervasyon
        // Seat ID - Which seat the reservation is for
        [Required(ErrorMessage = "Koltuk ID'si zorunludur")] // Zorunlu alan
        public int SeatId { get; set; }

        // Yolcu adı - Rezervasyon yapan kişinin adı
        // Passenger name - Name of the person making the reservation
        [Required(ErrorMessage = "Yolcu adı zorunludur")] // Zorunlu alan
        [StringLength(50)] // Maksimum 50 karakter
        [Display(Name = "Yolcu Adı")] // Görünen isim
        public string PassengerName { get; set; }

        // Yolcu soyadı - Rezervasyon yapan kişinin soyadı
        // Passenger surname - Surname of the person making the reservation
        [Required(ErrorMessage = "Yolcu soyadı zorunludur")] // Zorunlu alan
        [StringLength(50)] // Maksimum 50 karakter
        [Display(Name = "Yolcu Soyadı")] // Görünen isim
        public string PassengerSurname { get; set; }

        // PNR kodu - Benzersiz rezervasyon kodu (6 karakter)
        // PNR code - Unique reservation code (6 characters)
        [StringLength(10)] // Maksimum 10 karakter
        [Display(Name = "PNR Kodu")] // Görünen isim
        public string PNR { get; set; } // Uçuş Kodu - Kişiye özel

        // Rezervasyon tarihi - Rezervasyonun yapıldığı tarih ve saat
        // Booking date - Date and time when reservation was made
        [Required(ErrorMessage = "Rezervasyon tarihi zorunludur")] // Zorunlu alan
        [Display(Name = "Rezervasyon Tarihi")] // Görünen isim
        public DateTime BookingDate { get; set; }

        // Navigasyon özellikleri - İlişkili tablolar
        // Navigation properties - Related tables
        [ForeignKey("FlightId")] // Foreign key attribute'u
        public virtual Flight Flight { get; set; } // Uçuş bilgisi

        [ForeignKey("SeatId")] // Foreign key attribute'u
        public virtual Seat Seat { get; set; } // Koltuk bilgisi



        // Bilet alındıktan sonra fiyat değişse bile, fiyat sbt kalır
        public decimal Price { get; set; }
        public string CabinClass { get; set; }

        // Check-in properties
        public bool CheckedIn { get; set; }
        public DateTime? CheckInTime { get; set; }

        // Navigation property for meals
        public virtual ICollection<BookingMeal> BookingMeals { get; set; }

    }
}
