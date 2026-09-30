namespace Trabzon_Airlines.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddSeatBooking : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Bookings",
                c => new
                    {
                        BookingId = c.Int(nullable: false, identity: true),
                        FlightId = c.Int(nullable: false),
                        SeatId = c.Int(nullable: false),
                        PassengerName = c.String(),
                        PassengerSurname = c.String(),
                        PNR = c.String(),
                        BookingDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.BookingId)
                .ForeignKey("dbo.Flights", t => t.FlightId, cascadeDelete: true)
                .ForeignKey("dbo.Seats", t => t.SeatId, cascadeDelete: true)
                .Index(t => t.FlightId)
                .Index(t => t.SeatId);
            
            CreateTable(
                "dbo.Seats",
                c => new
                    {
                        SeatId = c.Int(nullable: false, identity: true),
                        SeatNumber = c.String(),
                        CabinClass = c.String(),
                    })
                .PrimaryKey(t => t.SeatId);
            
            CreateTable(
                "dbo.FlightSeats",
                c => new
                    {
                        FlightSeatId = c.Int(nullable: false, identity: true),
                        FlightId = c.Int(nullable: false),
                        SeatId = c.Int(nullable: false),
                        IsReserved = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.FlightSeatId)
                .ForeignKey("dbo.Flights", t => t.FlightId, cascadeDelete: true)
                .ForeignKey("dbo.Seats", t => t.SeatId, cascadeDelete: true)
                .Index(t => t.FlightId)
                .Index(t => t.SeatId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.FlightSeats", "SeatId", "dbo.Seats");
            DropForeignKey("dbo.FlightSeats", "FlightId", "dbo.Flights");
            DropForeignKey("dbo.Bookings", "SeatId", "dbo.Seats");
            DropForeignKey("dbo.Bookings", "FlightId", "dbo.Flights");
            DropIndex("dbo.FlightSeats", new[] { "SeatId" });
            DropIndex("dbo.FlightSeats", new[] { "FlightId" });
            DropIndex("dbo.Bookings", new[] { "SeatId" });
            DropIndex("dbo.Bookings", new[] { "FlightId" });
            DropTable("dbo.FlightSeats");
            DropTable("dbo.Seats");
            DropTable("dbo.Bookings");
        }
    }
}
