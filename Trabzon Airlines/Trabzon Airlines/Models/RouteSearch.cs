using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Trabzon_Airlines.Models
{
    // Kullanıcının hangi rotayı (IST-TZX gibi) kaç defa aradığını tutar
    // Tracks how many times a user searched a route
    public class RouteSearch
    {
        [Key]
        public int RouteSearchId { get; set; }

        [Index("IX_RouteSearch_User_Route", 1, IsUnique = true)]
        public int UserId { get; set; }

        [Required, StringLength(3)]
        [Index("IX_RouteSearch_User_Route", 2, IsUnique = true)]
        public string FromCode { get; set; }

        [Required, StringLength(3)]
        [Index("IX_RouteSearch_User_Route", 3, IsUnique = true)]
        public string ToCode { get; set; }

        public int SearchCount { get; set; }
        public DateTime FirstSearchedAt { get; set; }
        public DateTime LastSearchedAt { get; set; }
    }
}
