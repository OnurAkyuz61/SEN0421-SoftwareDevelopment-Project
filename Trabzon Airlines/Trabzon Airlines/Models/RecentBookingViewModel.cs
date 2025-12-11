using System; // DateTime için

namespace Trabzon_Airlines.Models
{
    // Son rezervasyonlar için view model sınıfı - Dashboard'da kullanılacak
    // Recent bookings view model class - To be used in Dashboard
    public class RecentBookingViewModel
    {
        // Rezervasyon ID'si
        // Booking ID
        public int BookingId { get; set; }

        // PNR kodu
        // PNR code
        public string PNR { get; set; }

        // Yolcu adı
        // Passenger name
        public string PassengerName { get; set; }

        // Yolcu soyadı
        // Passenger surname
        public string PassengerSurname { get; set; }

        // Rezervasyon tarihi
        // Booking date
        public DateTime BookingDate { get; set; }

        // Uçuş numarası
        // Flight number
        public string FlightNumber { get; set; }

        // Kalkış kodu
        // Departure code
        public string FromCode { get; set; }

        // Varış kodu
        // Arrival code
        public string ToCode { get; set; }
    }
}

