using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Trabzon_Airlines.Models
{
    // Kullanıcı dashboard'undaki tek bir rezervasyon satırı
    public class BookingListItemViewModel
    {
        public int BookingId { get; set; }
        public string PNR { get; set; }
        public string FlightNumber { get; set; }
        public string FromCode { get; set; }
        public string ToCode { get; set; }
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal Price { get; set; }
    }

    // Kullanıcı dashboard ekranı için model
    public class UserDashboardViewModel
    {
        public string FullName { get; set; }

        public int TotalBookings { get; set; }
        public int UpcomingCount { get; set; }
        public int PastCount { get; set; }
        public decimal TotalSpent { get; set; }

        public List<BookingListItemViewModel> UpcomingBookings { get; set; }
        public List<BookingListItemViewModel> PastBookings { get; set; }
    }

    // Şifre değiştirme formu için model
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Mevcut şifre zorunludur")]
        [DataType(DataType.Password)]
        [Display(Name = "Mevcut Şifre")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Yeni şifre zorunludur")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Şifre en az 6 karakter olmalıdır")]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni Şifre")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Yeni şifre tekrar zorunludur")]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni Şifre (Tekrar)")]
        [Compare("NewPassword", ErrorMessage = "Yeni şifre ile tekrar aynı olmalıdır")]
        public string ConfirmNewPassword { get; set; }
    }

    // Hesabım sayfası (profil + şifre)
    public class AccountViewModel
    {
        public int UserId { get; set; }

        [Display(Name = "Ad")]
        public string FirstName { get; set; }

        [Display(Name = "Soyad")]
        public string LastName { get; set; }

        [Display(Name = "E-posta")]
        public string Email { get; set; }

        [Display(Name = "Telefon")]
        public string Phone { get; set; }

        [Display(Name = "TC Kimlik No")]
        public string TcNumber { get; set; }

        [Display(Name = "Doğum Tarihi")]
        [DataType(DataType.Date)]
        public DateTime BirthDate { get; set; }

        [Display(Name = "Cinsiyet")]
        public string Gender { get; set; }

        public ChangePasswordViewModel ChangePassword { get; set; }
    }
}
