using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Trabzon_Airlines.Models
{
    // Kullanıcının hangi uçuşa kaç defa baktığını tutar (fiyat takibi için)
    // Tracks how many times a user viewed a flight (for price watching)
    public class FlightView
    {
        [Key]
        public int FlightViewId { get; set; }

        [Index("IX_FlightView_User_Flight", 1, IsUnique = true)]
        public int UserId { get; set; }

        [Index("IX_FlightView_User_Flight", 2, IsUnique = true)]
        public int FlightId { get; set; }

        // Kaç defa görüntülendi
        public int ViewCount { get; set; }

        public DateTime FirstViewedAt { get; set; }
        public DateTime LastViewedAt { get; set; }

        // Kullanıcının en son gördüğü fiyatlar (düşüş kıyası bunlara göre yapılır)
        public decimal LastSeenEconomyPrice { get; set; }
        public decimal LastSeenBusinessPrice { get; set; }

        [ForeignKey("FlightId")]
        public virtual Flight Flight { get; set; }
    }
}
