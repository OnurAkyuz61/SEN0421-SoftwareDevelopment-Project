using System; // DateTime için
using System.Collections.Generic; // List koleksiyonları için
using System.ComponentModel.DataAnnotations; // Data annotations için
using System.ComponentModel.DataAnnotations.Schema; // Database schema için
using System.Linq; // LINQ sorguları için
using System.Web; // HTTP context için

namespace Trabzon_Airlines.Models
{
    // Uçuş-Koltuk ilişki modeli - Her uçuş için koltuk durumları
    // Flight-Seat relationship model - Seat statuses for each flight
    public class FlightSeat
    {
        // Uçuş-Koltuk ID'si - Birincil anahtar
        // Flight-Seat ID - Primary key
        [Key] // Birincil anahtar attribute'u
        public int FlightSeatId { get; set; }

        // Uçuş ID'si - Hangi uçuş için
        // Flight ID - For which flight
        [Required(ErrorMessage = "Uçuş ID'si zorunludur")] // Zorunlu alan
        [Display(Name = "Uçuş")] // Görünen isim
        public int FlightId { get; set; }

        // Koltuk ID'si - Hangi koltuk için
        // Seat ID - For which seat
        [Required(ErrorMessage = "Koltuk ID'si zorunludur")] // Zorunlu alan
        [Display(Name = "Koltuk")] // Görünen isim
        public int SeatId { get; set; }

        // Rezerve edildi mi? - Koltuk rezerve edilmiş mi
        // Is reserved? - Is seat reserved
        [Display(Name = "Rezerve Edildi mi?")] // Görünen isim
        public bool IsReserved { get; set; } // True ise artık başkası seçemez

        // Navigasyon özellikleri - İlişkili tablolar
        // Navigation properties - Related tables
        [ForeignKey("FlightId")] // Foreign key attribute'u
        public virtual Flight Flight { get; set; } // Uçuş bilgisi

        [ForeignKey("SeatId")] // Foreign key attribute'u
        public virtual Seat Seat { get; set; } // Koltuk bilgisi
    }
}
