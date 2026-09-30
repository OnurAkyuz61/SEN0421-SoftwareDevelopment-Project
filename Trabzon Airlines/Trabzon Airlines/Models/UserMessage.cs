using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Trabzon_Airlines.Models
{
    // Kullanıcıya gönderilen site içi mesajlar (ör. fiyat düştü bildirimi)
    // In-site messages sent to users (e.g. price drop alerts)
    public class UserMessage
    {
        [Key]
        public int UserMessageId { get; set; }

        [Index]
        public int UserId { get; set; }

        // "PriceDrop" | "Info"
        [StringLength(30)]
        public string Type { get; set; }

        [Required, StringLength(200)]
        public string TitleTr { get; set; }

        [StringLength(200)]
        public string TitleEn { get; set; }

        [Required, StringLength(1000)]
        public string BodyTr { get; set; }

        [StringLength(1000)]
        public string BodyEn { get; set; }

        // Fiyat düşüşü detayları (Type = PriceDrop ise)
        public int? FlightId { get; set; }
        [StringLength(20)]
        public string CabinClass { get; set; }
        public decimal? OldPrice { get; set; }
        public decimal? NewPrice { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        // Not: Flight ile FK ilişkisi bilerek kurulmadı; admin uçuşu silse bile mesaj kalır.
    }
}
