using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class CatalogUniqueNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Benzersiz indeks, mevcut veritabanında aynı isimde iki kayıt varsa
            // oluşturulamaz. Önce isimler kırpılır; aynı isimde birden fazla kayıt
            // varsa en eski (en küçük Id) adını korur, diğerlerinin sonuna
            // " (#Id)" eklenir. Hiçbir kayıt silinmez, bağlı talepler etkilenmez;
            // yönetici sonradan Katalog ekranından adları düzeltebilir.
            foreach (var table in new[] { "Categories", "Departments", "Teams" })
            {
                migrationBuilder.Sql($@"
UPDATE [{table}] SET [Name] = LTRIM(RTRIM([Name])) WHERE [Name] <> LTRIM(RTRIM([Name]));

WITH d AS (
    SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [Name] ORDER BY [Id]) AS rn
    FROM [{table}]
)
UPDATE t SET [Name] = LEFT(t.[Name], 88) + N' (#' + CAST(t.[Id] AS nvarchar(10)) + N')'
FROM [{table}] t JOIN d ON d.[Id] = t.[Id]
WHERE d.rn > 1;");
            }

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Name",
                table: "Teams",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_Name",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Name",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");
        }
    }
}
