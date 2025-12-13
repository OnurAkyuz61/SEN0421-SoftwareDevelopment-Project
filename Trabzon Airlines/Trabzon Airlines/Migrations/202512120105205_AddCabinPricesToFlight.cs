namespace Trabzon_Airlines.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCabinPricesToFlight : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Flights", "EconomyPrice", c => c.Decimal(nullable: false, precision: 18, scale: 2));
            AddColumn("dbo.Flights", "BusinessPrice", c => c.Decimal(nullable: false, precision: 18, scale: 2));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Flights", "BusinessPrice");
            DropColumn("dbo.Flights", "EconomyPrice");
        }
    }
}
