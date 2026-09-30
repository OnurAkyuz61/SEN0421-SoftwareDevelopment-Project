using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Trabzon_Airlines.Models
{
    public class Meal
    {
        public int MealId { get; set; }

        [Required]
        [StringLength(100)]
        public string MealName { get; set; }

        [StringLength(50)]
        public string Category { get; set; }

        public decimal Price { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public bool IsAvailable { get; set; }

        [StringLength(200)]
        public string ImagePath { get; set; }

        public virtual ICollection<BookingMeal> BookingMeals { get; set; }
    }
}

