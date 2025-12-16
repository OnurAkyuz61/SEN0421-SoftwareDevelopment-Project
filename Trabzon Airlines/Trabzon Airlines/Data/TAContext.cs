using System; // DateTime için
using System.Collections.Generic; // List koleksiyonları için
using System.Data.Entity; // Entity Framework için
using System.Linq; // LINQ sorguları için
using System.Web; // HTTP context için
using Trabzon_Airlines.Models; // Model sınıfları için

namespace Trabzon_Airlines.Data
{
    // Veritabanı bağlam sınıfı - Entity Framework DbContext
    // Database context class - Entity Framework DbContext
    public class TAContext : DbContext
    {
        // Constructor - Veritabanı bağlantı adını belirt
        // Constructor - Specify database connection name
        public TAContext() : base("TAConn") { } // Web.config'deki connection string adı

        // Uçuşlar tablosu - Tüm uçuş kayıtları
        // Flights table - All flight records
        public DbSet<Flight> Flights { get; set; }

        // Koltuklar tablosu - Tüm koltuk kayıtları
        // Seats table - All seat records
        public DbSet<Seat> Seats { get; set; }

        // Uçuş-Koltuk ilişki tablosu - Her uçuş için koltuk durumları
        // Flight-Seat relationship table - Seat statuses for each flight
        public DbSet<FlightSeat> FlightSeats { get; set; }

        // Rezervasyonlar tablosu - Tüm rezervasyon kayıtları
        // Bookings table - All booking records
        public DbSet<Booking> Bookings { get; set; }

        // Kullanıcılar tablosu - Tüm kullanıcı kayıtları
        // Users table - All user records
        public DbSet<User> Users { get; set; }

        // Adminler tablosu - Tüm admin kayıtları
        // Admins table - All admin records
        public DbSet<Admin> Admins { get; set; }

        //Destek talepleri tablosu-Ticketlar
        public DbSet<SupportRequest> SupportRequests { get; set; }

        // Meals tablosu - Tüm yemek kayıtları
        // Meals table - All meal records
        public DbSet<Meal> Meals { get; set; }

        // BookingMeals tablosu - Rezervasyon-yemek ilişki tablosu
        // BookingMeals table - Booking-meal relationship table
        public DbSet<BookingMeal> BookingMeals { get; set; }
    }
}
