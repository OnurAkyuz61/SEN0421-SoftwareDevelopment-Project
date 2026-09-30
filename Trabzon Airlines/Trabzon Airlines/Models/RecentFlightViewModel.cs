using System; // DateTime ve Decimal için

namespace Trabzon_Airlines.Models
{
    // Son uçuşlar için view model sınıfı - Dashboard'da kullanılacak
    // Recent flights view model class - To be used in Dashboard
    public class RecentFlightViewModel
    {
        // Uçuş ID'si
        // Flight ID
        public int FlightId { get; set; }

        // Uçuş numarası
        // Flight number
        public string FlightNumber { get; set; }

        // Kalkış kodu
        // Departure code
        public string FromCode { get; set; }

        // Varış kodu
        // Arrival code
        public string ToCode { get; set; }

        // Kalkış zamanı
        // Departure time
        public DateTime DepartureTime { get; set; }

        // Varış zamanı
        // Arrival time
        public DateTime ArrivalTime { get; set; }

        // Fiyat
        // Price
        public decimal Price { get; set; }
    }
}

