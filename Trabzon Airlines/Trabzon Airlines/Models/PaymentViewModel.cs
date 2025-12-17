using System; // Decimal için
using System.ComponentModel.DataAnnotations; // Validasyon özellikleri için

namespace Trabzon_Airlines.Models
{
    // Ödeme view model sınıfı - Ödeme formu için kullanılacak model
    // Payment view model class - Model to be used for payment form
    public class PaymentViewModel
    {
        // Ödeme tutarı - Varsayılan değer 1050.00 TL
        // Payment amount - Default value 1050.00 TL
        // Not: Amount validasyonu controller'da manuel yapılıyor (model binding sorunlarını önlemek için)
        // Note: Amount validation is done manually in controller (to prevent model binding issues)
        public decimal Amount { get; set; } = 1050.00m; // Varsayılan tutar - Default amount

        // Formdan gelecek veriler
        // Data coming from form

        // Kart sahibinin adı soyadı
        // Card holder's full name
        [Required(ErrorMessage = "Kart sahibinin adı soyadı gereklidir.")] // Zorunlu alan - Required field
        [StringLength(100, ErrorMessage = "Kart sahibinin adı soyadı en fazla 100 karakter olabilir.")] // Maksimum uzunluk - Maximum length
        public string CardHolderName { get; set; }

        // Kart numarası
        // Card number
        [Required(ErrorMessage = "Kart numarası gereklidir.")] // Zorunlu alan - Required field
        // Not: RegularExpression boşluklu formatı kabul etmez, bu yüzden controller'da temizleniyor
        // Note: RegularExpression doesn't accept spaced format, so it's cleaned in controller
        public string CardNumber { get; set; }

        // Son kullanma tarihi (Format: AA/YY - Örn: 12/25)
        // Expiry date (Format: MM/YY - e.g., 12/25)
        [Required(ErrorMessage = "Son kullanma tarihi gereklidir.")] // Zorunlu alan - Required field
        [RegularExpression(@"^(0[1-9]|1[0-2])\/([0-9]{2})$", ErrorMessage = "Son kullanma tarihi AA/YY formatında olmalıdır (örn: 12/25).")] // Tarih formatı - Date format
        public string ExpireDate { get; set; }

        // CVC/CVV güvenlik kodu
        // CVC/CVV security code
        [Required(ErrorMessage = "CVC kodu gereklidir.")] // Zorunlu alan - Required field
        [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVC kodu 3 veya 4 haneli olmalıdır.")] // CVC formatı - CVC format
        public string Cvc { get; set; }

        // 3D Secure kullanımı (şu an kullanılmıyor ama gelecekte kullanılabilir)
        // 3D Secure usage (not currently used but can be used in the future)
        public bool Use3DSecure { get; set; }

        // Ekstra Bagaj Hizmeti (+250 TL)
        // Extra Baggage Service (+250 TL)
        public bool HasExtraBaggage { get; set; }

        // Uçak İçi Wi-Fi Hizmeti (+150 TL)
        // In-flight Wi-Fi Service (+150 TL)
        public bool HasWifi { get; set; }

        // Özel Yemek Seçimi (+200 TL)
        // Special Meal Selection (+200 TL)
        public bool HasMeal { get; set; }

        // Hızlı Geçiş / Fast Track (+300 TL)
        // Fast Track Service (+300 TL)
        public bool HasFastTrack { get; set; }

        // ---------------------------------------------------------

        // 3D Secure kullanımı (3D sistemi kaldırıldığı için false gelebilir ama modelde tutuyoruz)
        // 3D Secure usage (Currently removed but kept in model for future use)
        
    }
}

