using System; // DateTime için
using System.Collections.Generic; // List koleksiyonları için
using System.ComponentModel.DataAnnotations; // Data annotations için
using System.Linq; // LINQ sorguları için
using System.Web; // HTTP context için

namespace Trabzon_Airlines.Models
{
    // Uçuş modeli - Havayolu seferleri için
    // Flight model - For airline flights
    public class Flight
    {
        // Uçuş ID'si - Birincil anahtar
        // Flight ID - Primary key
        [Key] // Birincil anahtar attribute'u
        public int FlightId { get; set; }

        // Sefer numarası - Uçuşun benzersiz numarası
        // Flight number - Unique number of the flight
        [Display(Name = "Sefer No")] // Görünen isim
        [Required(ErrorMessage = "Sefer numarası zorunludur")] // Zorunlu alan
        [StringLength(20)] // Maksimum 20 karakter
        public string FlightNumber { get; set; }

        // Kalkış havalimanı IATA kodu - Örn: TZX, IST
        // Departure airport IATA code - E.g.: TZX, IST
        [Display(Name = "Kalkış (IATA Kodu)")] // Görünen isim
        [Required(ErrorMessage = "Kalkış havalimanı kodu zorunludur")] // Zorunlu alan
        [StringLength(3)] // IATA kodu 3 karakter
        public string FromCode { get; set; }

        // Varış havalimanı IATA kodu - Örn: IST, TZX
        // Arrival airport IATA code - E.g.: IST, TZX
        [Display(Name = "Varış (IATA Kodu)")] // Görünen isim
        [Required(ErrorMessage = "Varış havalimanı kodu zorunludur")] // Zorunlu alan
        [StringLength(3)] // IATA kodu 3 karakter
        public string ToCode { get; set; }

        // Kalkış zamanı - Uçuşun kalkış tarihi ve saati
        // Departure time - Flight departure date and time
        [Display(Name = "Kalkış Zamanı")] // Görünen isim
        [Required(ErrorMessage = "Kalkış zamanı zorunludur")] // Zorunlu alan
        public DateTime DepartureTime { get; set; }

        // Varış zamanı - Uçuşun varış tarihi ve saati
        // Arrival time - Flight arrival date and time
        [Display(Name = "Varış Zamanı")] // Görünen isim
        [Required(ErrorMessage = "Varış zamanı zorunludur")] // Zorunlu alan
        public DateTime ArrivalTime { get; set; }

        // Uçuş fiyatı - Bilet fiyatı
        // Flight price - Ticket price
        [Display(Name = "Fiyat")] // Görünen isim
        [Required(ErrorMessage = "Fiyat zorunludur")] // Zorunlu alan
        [Range(0, double.MaxValue, ErrorMessage = "Fiyat 0'dan büyük olmalıdır")] // Fiyat pozitif olmalı
        public decimal Price { get; set; }

        // Aktif mi? - Uçuş aktif mi pasif mi
        // Is active? - Is flight active or inactive
        [Display(Name = "Aktif mi?")] // Görünen isim
        public bool IsActive { get; set; } = true; // Varsayılan değer: true

        // Hava durumu - Güneşli, Yağmurlu, Karlı
        // Weather condition - Sunny, Rainy, Snowy
        [Display(Name = "Hava Durumu")] // Görünen isim
        [StringLength(50)] // Maksimum 50 karakter
        public string WeatherCondition { get; set; }

        // Rötar riski - Düşük, Orta, Yüksek
        // Delay risk - Low, Medium, High
        [Display(Name = "Rötar Riski")] // Görünen isim
        [StringLength(20)] // Maksimum 20 karakter
        public string DelayRisk { get; set; }
    }
}
