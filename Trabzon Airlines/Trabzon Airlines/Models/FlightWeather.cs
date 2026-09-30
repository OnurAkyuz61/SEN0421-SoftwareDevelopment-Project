using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Trabzon_Airlines.Models
{
    // Bir uçuşun kalkış ve varış havalimanındaki hava tahmini + hesaplanan rötar riski
    // Weather forecast at departure/arrival airports + computed delay risk for a flight
    public class FlightWeather
    {
        [Key]
        public int FlightWeatherId { get; set; }

        [Index(IsUnique = true)]
        public int FlightId { get; set; }

        // Kalkış havalimanı (kalkış saatindeki tahmin)
        [StringLength(50)] public string FromLabel { get; set; }      // "Karlı"
        public double? FromTempC { get; set; }
        public double? FromWindKmh { get; set; }
        public double? FromGustKmh { get; set; }
        public double? FromVisibilityM { get; set; }
        public double? FromPrecipMm { get; set; }
        public double? FromSnowCm { get; set; }

        // Varış havalimanı (varış saatindeki tahmin)
        [StringLength(50)] public string ToLabel { get; set; }
        public double? ToTempC { get; set; }
        public double? ToWindKmh { get; set; }
        public double? ToGustKmh { get; set; }
        public double? ToVisibilityM { get; set; }
        public double? ToPrecipMm { get; set; }
        public double? ToSnowCm { get; set; }

        // 0 = Düşük, 1 = Orta, 2 = Yüksek
        public int RiskLevel { get; set; }

        // Riskin nedenleri (okunabilir metin)
        [StringLength(500)] public string ReasonsTr { get; set; }
        [StringLength(500)] public string ReasonsEn { get; set; }

        // Yolculara en son hangi seviyede uyarı gönderildi (-1 = hiç)
        public int LastAlertLevel { get; set; } = -1;

        // Test için elle oluşturulan simülasyon mu?
        public bool IsSimulated { get; set; }

        public DateTime UpdatedAt { get; set; }

        [ForeignKey("FlightId")]
        public virtual Flight Flight { get; set; }
    }
}
