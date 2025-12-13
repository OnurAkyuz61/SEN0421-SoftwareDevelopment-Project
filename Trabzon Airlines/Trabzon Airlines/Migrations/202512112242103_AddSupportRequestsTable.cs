namespace Trabzon_Airlines.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddSupportRequestsTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.SupportRequests",
                c => new
                    {
                        SupportRequestId = c.Int(nullable: false, identity: true),
                        UserId = c.Int(),
                        FullName = c.String(nullable: false, maxLength: 100),
                        Email = c.String(nullable: false, maxLength: 100),
                        Topic = c.String(nullable: false, maxLength: 50),
                        PNR = c.String(maxLength: 10),
                        Message = c.String(nullable: false, maxLength: 1000),
                        Status = c.String(maxLength: 20),
                        CreatedAt = c.DateTime(nullable: false),
                        UpdatedAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.SupportRequestId)
                .ForeignKey("dbo.Users", t => t.UserId)
                .Index(t => t.UserId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.SupportRequests", "UserId", "dbo.Users");
            DropIndex("dbo.SupportRequests", new[] { "UserId" });
            DropTable("dbo.SupportRequests");
        }
    }
}
