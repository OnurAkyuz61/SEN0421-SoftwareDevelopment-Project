using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Trabzon_Airlines.Models
{
    // Müşteri destek talepleri için model
    public class SupportRequest
    {
        [Key]
        public int SupportRequestId { get; set; }   // Ticket ID

        // Opsiyonel: login olan kullanıcı ile ilişki
        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Ad Soyad")]
        public string FullName { get; set; }

        [Required, StringLength(100)]
        [EmailAddress]
        [Display(Name = "E-posta")]
        public string Email { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Konu")]
        public string Topic { get; set; }   // reservation / payment / ...

        [StringLength(10)]
        [Display(Name = "PNR Kodu")]
        public string PNR { get; set; }

        [Required, StringLength(1000)]
        [Display(Name = "Mesaj")]
        public string Message { get; set; }

        [StringLength(20)]
        [Display(Name = "Durum")]
        public string Status { get; set; } = "New"; // New / InProgress / Resolved

        [Display(Name = "Oluşturulma Tarihi")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Güncellenme Tarihi")]
        public DateTime? UpdatedAt { get; set; }
    }
}
