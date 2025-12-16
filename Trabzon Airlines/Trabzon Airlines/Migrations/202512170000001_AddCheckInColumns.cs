namespace Trabzon_Airlines.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCheckInColumns : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Bookings", "CheckedIn", c => c.Boolean(nullable: false));
            AddColumn("dbo.Bookings", "CheckInTime", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Bookings", "CheckInTime");
            DropColumn("dbo.Bookings", "CheckedIn");
        }
    }
}

