namespace Trabzon_Airlines.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddFlightStatusFields : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Flights", "WeatherCondition", c => c.String());
            AddColumn("dbo.Flights", "DelayRisk", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Flights", "DelayRisk");
            DropColumn("dbo.Flights", "WeatherCondition");
        }
    }
}
