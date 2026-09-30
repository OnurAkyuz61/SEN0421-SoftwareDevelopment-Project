namespace Trabzon_Airlines.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPriceToBooking : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Bookings", "Price", c => c.Decimal(nullable: false, precision: 18, scale: 2));
            AddColumn("dbo.Bookings", "CabinClass", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Bookings", "CabinClass");
            DropColumn("dbo.Bookings", "Price");
        }
    }
}
