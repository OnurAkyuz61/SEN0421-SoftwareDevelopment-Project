using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Trabzon_Airlines.Models
{
    public class BookingMeal
    {
        [Key]
        public int BookingMealId { get; set; }

        [Required]
        public int BookingId { get; set; }

        [Required]
        public int MealId { get; set; }

        public int Quantity { get; set; } = 1;

        [ForeignKey("BookingId")]
        public virtual Booking Booking { get; set; }

        [ForeignKey("MealId")]
        public virtual Meal Meal { get; set; }
    }
}

