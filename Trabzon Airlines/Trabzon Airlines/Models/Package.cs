using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Trabzon_Airlines.Models
{
    // Paket modeli - Seyahat paketleri için
    // Package model - For travel packages
    public class Package
    {
        [Key] // Birincil anahtar
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Otomatik artan ID
        public int Id { get; set; }

        [Required(ErrorMessage = "Paket adı zorunludur")]
        [StringLength(200)]
        [Display(Name = "Paket Adı")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Açıklama zorunludur")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Açıklama")]
        public string Description { get; set; }

        [StringLength(500)]
        [Display(Name = "Kısa Açıklama")]
        public string? ShortDescription { get; set; } // Kısa açıklama (liste görünümü için)

        [StringLength(500)]
        [Display(Name = "Görsel URL")]
        public string? ImageUrl { get; set; } // Paket görseli

        [Required(ErrorMessage = "Fiyat zorunludur")]
        [Display(Name = "Fiyat")]
        [Column(TypeName = "decimal(18,2)")] // Para birimi için decimal
        public decimal Price { get; set; }

        [Display(Name = "Eski Fiyat")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? OldPrice { get; set; } // İndirimli fiyat için eski fiyat

        [Display(Name = "İndirim Yüzdesi")]
        public int? DiscountPercentage { get; set; } // İndirim yüzdesi

        [Required(ErrorMessage = "Kategori zorunludur")]
        [StringLength(50)]
        [Display(Name = "Kategori")]
        public string Category { get; set; } // domestic, international, beach, city, adventure

        [StringLength(50)]
        [Display(Name = "Alt Kategori")]
        public string? SubCategory { get; set; } // Ek kategori bilgisi

        [Display(Name = "Süre (Gün)")]
        public int Duration { get; set; } // Paket süresi (gün)

        [Display(Name = "Gece Sayısı")]
        public int Nights { get; set; } // Gece sayısı

        [Display(Name = "Puan")]
        public decimal Rating { get; set; } // Kullanıcı puanı (0-5)

        [Display(Name = "Puan Sayısı")]
        public int RatingCount { get; set; } // Kaç kişi puanladı

        [StringLength(50)]
        [Display(Name = "Rozet")]
        public string? Badge { get; set; } // Popüler, Yeni, Özel, İndirim

        [Display(Name = "Özellikler")]
        public string? Features { get; set; } // JSON veya virgülle ayrılmış özellikler

        [Display(Name = "Otel Yıldızı")]
        public int? HotelStars { get; set; } // Otel yıldız sayısı

        [Display(Name = "Uçak Bileti Dahil")]
        public bool FlightIncluded { get; set; } // Uçak bileti dahil mi

        [Display(Name = "Yemek Tipi")]
        [StringLength(50)]
        public string? MealType { get; set; } // Kahvaltı, Her Şey Dahil, vb.

        [Display(Name = "Aktif Mi")]
        public bool IsActive { get; set; } // Paket aktif mi

        [Display(Name = "Öne Çıkan")]
        public bool IsFeatured { get; set; } // Öne çıkan paket mi

        [Display(Name = "Oluşturulma Tarihi")]
        public DateTime CreatedAt { get; set; } // Oluşturulma tarihi

        [Display(Name = "Güncelleme Tarihi")]
        public DateTime? UpdatedAt { get; set; } // Güncelleme tarihi

        // Navigasyon özellikleri
        // Navigation properties
        public virtual ICollection<PackageReservation> Reservations { get; set; } // Rezervasyonlar
    }
}

