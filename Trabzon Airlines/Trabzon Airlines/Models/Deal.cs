using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Trabzon_Airlines.Models
{
    // Fırsat modeli - Özel kampanyalar ve fırsatlar için
    // Deal model - For special campaigns and deals
    public class Deal
    {
        [Key] // Birincil anahtar
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Otomatik artan ID
        public int Id { get; set; }

        [Required(ErrorMessage = "Fırsat adı zorunludur")]
        [StringLength(200)]
        [Display(Name = "Fırsat Adı")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Açıklama zorunludur")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Açıklama")]
        public string Description { get; set; }

        [StringLength(500)]
        [Display(Name = "Kısa Açıklama")]
        public string? ShortDescription { get; set; } // Kısa açıklama

        [StringLength(500)]
        [Display(Name = "Görsel URL")]
        public string? ImageUrl { get; set; } // Fırsat görseli

        [Required(ErrorMessage = "İndirim yüzdesi zorunludur")]
        [Display(Name = "İndirim Yüzdesi")]
        public int DiscountPercentage { get; set; } // İndirim yüzdesi

        [StringLength(50)]
        [Display(Name = "Rozet")]
        public string? Badge { get; set; } // Sıcak Fırsat, Yeni, Son Dakika, Özel, vb.

        [StringLength(50)]
        [Display(Name = "Kampanya Kodu")]
        public string? CampaignCode { get; set; } // Kampanya kodu (ERKEN2024 gibi)

        [Display(Name = "Başlangıç Tarihi")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } // Kampanya başlangıç tarihi

        [Display(Name = "Bitiş Tarihi")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } // Kampanya bitiş tarihi

        [Display(Name = "Minimum Kişi Sayısı")]
        public int? MinPersonCount { get; set; } // Minimum kişi sayısı (grup indirimleri için)

        [Display(Name = "Maksimum Kişi Sayısı")]
        public int? MaxPersonCount { get; set; } // Maksimum kişi sayısı

        [Display(Name = "Öğrenci İndirimi")]
        public bool IsStudentDiscount { get; set; } // Öğrenci indirimi mi

        [Display(Name = "Aile İndirimi")]
        public bool IsFamilyDiscount { get; set; } // Aile indirimi mi

        [Display(Name = "Grup İndirimi")]
        public bool IsGroupDiscount { get; set; } // Grup indirimi mi

        [Display(Name = "Aktif Mi")]
        public bool IsActive { get; set; } // Fırsat aktif mi

        [Display(Name = "Öne Çıkan")]
        public bool IsFeatured { get; set; } // Öne çıkan fırsat mı

        [Display(Name = "Oluşturulma Tarihi")]
        public DateTime CreatedAt { get; set; } // Oluşturulma tarihi

        [Display(Name = "Güncelleme Tarihi")]
        public DateTime? UpdatedAt { get; set; } // Güncelleme tarihi

        // Navigasyon özellikleri
        // Navigation properties
        public virtual ICollection<DealReservation> Reservations { get; set; } // Rezervasyonlar
    }
}

