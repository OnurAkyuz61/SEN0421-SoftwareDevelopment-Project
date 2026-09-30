namespace Trabzon_Airlines.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCheckInColumns : DbMigration
    {
        public override void Up()
        {
            // Önce kolonu nullable olarak ekle
            // First add column as nullable
            AddColumn("dbo.Bookings", "CheckedIn", c => c.Boolean());
            AddColumn("dbo.Bookings", "CheckInTime", c => c.DateTime());
            
            // Mevcut kayıtlar için varsayılan değerleri ayarla
            // Set default values for existing records
            Sql("UPDATE dbo.Bookings SET CheckedIn = 0 WHERE CheckedIn IS NULL");
            
            // Şimdi kolonu non-nullable yap
            // Now make column non-nullable
            AlterColumn("dbo.Bookings", "CheckedIn", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Bookings", "CheckInTime");
            DropColumn("dbo.Bookings", "CheckedIn");
        }
    }
}

