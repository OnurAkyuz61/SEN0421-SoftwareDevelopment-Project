using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class CatalogAndOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Category",
                table: "Tickets",
                newName: "CategoryId");

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedTeamId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    LeadUserId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Teams_Users_LeadUserId",
                        column: x => x.LeadUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    DefaultTeamId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Categories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Categories_Teams_DefaultTeamId",
                        column: x => x.DefaultTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "DefaultTeamId", "IsActive", "Name", "ParentId", "SortOrder" },
                values: new object[] { 1, null, true, "Diğer", null, 99 });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, true, "Bilgi İşlem" },
                    { 2, true, "İnsan Kaynakları" },
                    { 3, true, "Muhasebe" },
                    { 4, true, "Satış" }
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DepartmentId", "TeamId" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DepartmentId", "TeamId" },
                values: new object[] { 1, 1 });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "DepartmentId", "TeamId" },
                values: new object[] { 2, null });

            migrationBuilder.InsertData(
                table: "Teams",
                columns: new[] { "Id", "DepartmentId", "IsActive", "LeadUserId", "Name" },
                values: new object[,]
                {
                    { 1, 1, true, null, "Son Kullanıcı Desteği" },
                    { 2, 1, true, null, "Ağ ve Sistem" }
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "DefaultTeamId", "IsActive", "Name", "ParentId", "SortOrder" },
                values: new object[,]
                {
                    { 2, 1, true, "Donanım", null, 10 },
                    { 3, 1, true, "Yazılım", null, 20 },
                    { 4, 2, true, "Ağ / İnternet", null, 30 },
                    { 5, 2, true, "Hesap / Erişim", null, 40 },
                    { 6, 1, true, "E-posta", null, 50 }
                });

            // Kolon yeniden adlandırıldı ama içinde hâlâ eski TicketCategory enum
            // değerleri (0-5) duruyor. Kategori Id'leri bu değerlerin bir fazlası
            // olacak şekilde seedlendiği için tek bir kaydırma yeterli.
            // Yabancı anahtar aşağıda, bu taşımadan SONRA ekleniyor.
            migrationBuilder.Sql("UPDATE Tickets SET CategoryId = CategoryId + 1;");

            // Kategorinin varsayılan ekibi mevcut taleplere de uygulanır
            migrationBuilder.Sql(@"
                UPDATE t
                   SET t.AssignedTeamId = c.DefaultTeamId
                  FROM Tickets t
                  JOIN Categories c ON c.Id = t.CategoryId
                 WHERE t.AssignedTeamId IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DepartmentId",
                table: "Users",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TeamId",
                table: "Users",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedTeamId",
                table: "Tickets",
                column: "AssignedTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CategoryId",
                table: "Tickets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_DefaultTeamId",
                table: "Categories",
                column: "DefaultTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_IsActive_SortOrder",
                table: "Categories",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentId",
                table: "Categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_DepartmentId",
                table: "Teams",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_LeadUserId",
                table: "Teams",
                column: "LeadUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Categories_CategoryId",
                table: "Tickets",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Teams_AssignedTeamId",
                table: "Tickets",
                column: "AssignedTeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                table: "Users",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Teams_TeamId",
                table: "Users",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Categories_CategoryId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Teams_AssignedTeamId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Teams_TeamId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Users_DepartmentId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TeamId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_AssignedTeamId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_CategoryId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AssignedTeamId",
                table: "Tickets");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "Tickets",
                newName: "Category");

            // Kategori Id'leri eski enum değerlerine geri çevrilir
            migrationBuilder.Sql("UPDATE Tickets SET Category = CASE WHEN Category > 0 THEN Category - 1 ELSE 0 END;");
        }
    }
}
