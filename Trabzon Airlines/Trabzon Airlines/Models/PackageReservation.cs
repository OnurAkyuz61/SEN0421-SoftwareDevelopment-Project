using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Trabzon_Airlines.Models
{
    // Paket rezervasyon modeli - Kullanıcıların paket rezervasyonları için
    // Package reservation model - For user package reservations
    public class PackageReservation
    {
        [Key] // Birincil anahtar
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Otomatik artan ID
        public int Id { get; set; }

        [Required]
        [Display(Name = "Kullanıcı")]
        public int UserId { get; set; } // Kullanıcı ID

        [Required]
        [Display(Name = "Paket")]
        public int PackageId { get; set; } // Paket ID

        [Required]
        [Display(Name = "Kişi Sayısı")]
        public int PersonCount { get; set; } // Kaç kişi

        [Required]
        [Display(Name = "Toplam Fiyat")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; } // Toplam fiyat

        [Display(Name = "Rezervasyon Tarihi")]
        public DateTime ReservationDate { get; set; } // Rezervasyon tarihi

        [Display(Name = "Seyahat Tarihi")]
        [DataType(DataType.Date)]
        public DateTime TravelDate { get; set; } // Seyahat tarihi

        [StringLength(50)]
        [Display(Name = "Durum")]
        public string Status { get; set; } // Beklemede, Onaylandı, İptal Edildi

        [Display(Name = "Oluşturulma Tarihi")]
        public DateTime CreatedAt { get; set; } // Oluşturulma tarihi

        // Navigasyon özellikleri
        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } // Kullanıcı

        [ForeignKey("PackageId")]
        public virtual Package Package { get; set; } // Paket
    }
}

