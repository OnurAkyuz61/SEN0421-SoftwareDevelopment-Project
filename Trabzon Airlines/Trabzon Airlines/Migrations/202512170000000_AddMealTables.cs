namespace Trabzon_Airlines.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddMealTables : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Meals",
                c => new
                    {
                        MealId = c.Int(nullable: false, identity: true),
                        MealName = c.String(nullable: false, maxLength: 100),
                        Category = c.String(maxLength: 50),
                        Price = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Description = c.String(maxLength: 500),
                        IsAvailable = c.Boolean(nullable: false),
                        ImagePath = c.String(maxLength: 200),
                    })
                .PrimaryKey(t => t.MealId);
            
            CreateTable(
                "dbo.BookingMeals",
                c => new
                    {
                        BookingMealId = c.Int(nullable: false, identity: true),
                        BookingId = c.Int(nullable: false),
                        MealId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.BookingMealId)
                .ForeignKey("dbo.Bookings", t => t.BookingId, cascadeDelete: true)
                .ForeignKey("dbo.Meals", t => t.MealId, cascadeDelete: true)
                .Index(t => t.BookingId)
                .Index(t => t.MealId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.BookingMeals", "MealId", "dbo.Meals");
            DropForeignKey("dbo.BookingMeals", "BookingId", "dbo.Bookings");
            DropIndex("dbo.BookingMeals", new[] { "MealId" });
            DropIndex("dbo.BookingMeals", new[] { "BookingId" });
            DropTable("dbo.BookingMeals");
            DropTable("dbo.Meals");
        }
    }
}

