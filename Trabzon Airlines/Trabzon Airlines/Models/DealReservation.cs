using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Trabzon_Airlines.Models
{
    // Fırsat rezervasyon modeli - Kullanıcıların fırsat rezervasyonları için
    // Deal reservation model - For user deal reservations
    public class DealReservation
    {
        [Key] // Birincil anahtar
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Otomatik artan ID
        public int Id { get; set; }

        [Required]
        [Display(Name = "Kullanıcı")]
        public int UserId { get; set; } // Kullanıcı ID

        [Required]
        [Display(Name = "Fırsat")]
        public int DealId { get; set; } // Fırsat ID

        [Required]
        [Display(Name = "Kişi Sayısı")]
        public int PersonCount { get; set; } // Kaç kişi

        [Display(Name = "Kampanya Kodu")]
        [StringLength(50)]
        public string? CampaignCode { get; set; } // Kullanılan kampanya kodu

        [Display(Name = "Rezervasyon Tarihi")]
        public DateTime ReservationDate { get; set; } // Rezervasyon tarihi

        [StringLength(50)]
        [Display(Name = "Durum")]
        public string Status { get; set; } // Beklemede, Onaylandı, İptal Edildi

        [Display(Name = "Oluşturulma Tarihi")]
        public DateTime CreatedAt { get; set; } // Oluşturulma tarihi

        // Navigasyon özellikleri
        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } // Kullanıcı

        [ForeignKey("DealId")]
        public virtual Deal Deal { get; set; } // Fırsat
    }
}

