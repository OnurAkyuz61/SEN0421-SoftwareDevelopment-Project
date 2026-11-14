using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class ServiceItemCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServiceItemId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    DefaultTeamId = table.Column<int>(type: "int", nullable: true),
                    DefaultPriority = table.Column<int>(type: "int", nullable: true),
                    DefaultType = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceItems_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceItems_Teams_DefaultTeamId",
                        column: x => x.DefaultTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ServiceItems",
                columns: new[] { "Id", "CategoryId", "Code", "DefaultPriority", "DefaultTeamId", "DefaultType", "Description", "IsActive", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, 2, "HW-LAPTOP", 1, null, 1, null, true, "Yeni laptop talebi", 10 },
                    { 2, 2, "HW-PRINT", 2, null, 0, null, true, "Yazıcı arızası", 20 },
                    { 3, 5, "ACC-VPN", 1, null, 1, null, true, "VPN erişimi", 10 },
                    { 4, 3, "SW-INST", null, null, 1, null, true, "Yazılım kurulumu", 10 },
                    { 5, 4, "NET-DOWN", 3, 2, 0, null, true, "İnternet kesintisi", 10 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ServiceItemId",
                table: "Tickets",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItems_CategoryId",
                table: "ServiceItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItems_Code",
                table: "ServiceItems",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItems_DefaultTeamId",
                table: "ServiceItems",
                column: "DefaultTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItems_IsActive_SortOrder",
                table: "ServiceItems",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItems_Name",
                table: "ServiceItems",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_ServiceItems_ServiceItemId",
                table: "Tickets",
                column: "ServiceItemId",
                principalTable: "ServiceItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_ServiceItems_ServiceItemId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "ServiceItems");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ServiceItemId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ServiceItemId",
                table: "Tickets");
        }
    }
}
