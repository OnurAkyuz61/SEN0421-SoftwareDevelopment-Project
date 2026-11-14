using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class PasswordMaxAge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            // Mevcut hesaplar migration anında yenilenmiş sayılsın; aksi halde
            // CreatedAt eski olanlar hemen şifre yenileme kilidine düşerdi.
            migrationBuilder.Sql(
                "UPDATE Users SET PasswordChangedAt = SYSUTCDATETIME() WHERE PasswordChangedAt IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                table: "Users");
        }
    }
}
