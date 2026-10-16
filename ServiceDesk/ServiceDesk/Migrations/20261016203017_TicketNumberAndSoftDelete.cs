using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class TicketNumberAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletedById",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Number",
                table: "Tickets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Mevcut talepler de numaralandırılır. Yıl, tarih UTC saklandığı için
            // yerel saate çevrilerek alınır; biçim uygulamadakiyle aynıdır.
            migrationBuilder.Sql(@"
                UPDATE Tickets
                   SET Number = CASE WHEN Type = 1 THEN 'CHG-' ELSE 'INC-' END
                              + CAST(YEAR(CAST(CreatedAt AT TIME ZONE 'UTC'
                                    AT TIME ZONE 'Turkey Standard Time' AS datetime2)) AS varchar(4))
                              + '-' + RIGHT('00000' + CAST(Id AS varchar(10)), 5)
                 WHERE Number IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_DeletedById",
                table: "Tickets",
                column: "DeletedById");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Number",
                table: "Tickets",
                column: "Number",
                unique: true,
                filter: "[Number] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_DeletedById",
                table: "Tickets",
                column: "DeletedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_DeletedById",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_DeletedById",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_Number",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Tickets");
        }
    }
}
