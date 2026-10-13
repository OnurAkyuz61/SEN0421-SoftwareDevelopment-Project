using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class PortalVeYonetim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalDecidedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalNote",
                table: "Tickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequesterId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RequesterId",
                table: "Tickets",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TicketId",
                table: "Attachments",
                column: "TicketId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_RequesterId",
                table: "Tickets",
                column: "RequesterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_RequesterId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_RequesterId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ApprovalDecidedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ApprovalNote",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RequesterId",
                table: "Tickets");
        }
    }
}
