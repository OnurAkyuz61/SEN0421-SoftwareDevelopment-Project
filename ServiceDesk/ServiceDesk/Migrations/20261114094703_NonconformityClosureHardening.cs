using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class NonconformityClosureHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArticleId",
                table: "Nonconformities",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationNote",
                table: "Nonconformities",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "Nonconformities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerifiedById",
                table: "Nonconformities",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NonconformityAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NonconformityId = table.Column<int>(type: "int", nullable: false),
                    UploadedById = table.Column<int>(type: "int", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScanStatus = table.Column<int>(type: "int", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NonconformityAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NonconformityAttachments_Nonconformities_NonconformityId",
                        column: x => x.NonconformityId,
                        principalTable: "Nonconformities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NonconformityAttachments_Users_DeletedById",
                        column: x => x.DeletedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NonconformityAttachments_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Nonconformities_ArticleId",
                table: "Nonconformities",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_Nonconformities_VerifiedById",
                table: "Nonconformities",
                column: "VerifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_NonconformityAttachments_DeletedById",
                table: "NonconformityAttachments",
                column: "DeletedById");

            migrationBuilder.CreateIndex(
                name: "IX_NonconformityAttachments_NonconformityId",
                table: "NonconformityAttachments",
                column: "NonconformityId");

            migrationBuilder.CreateIndex(
                name: "IX_NonconformityAttachments_UploadedById",
                table: "NonconformityAttachments",
                column: "UploadedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Nonconformities_KnowledgeArticles_ArticleId",
                table: "Nonconformities",
                column: "ArticleId",
                principalTable: "KnowledgeArticles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Nonconformities_Users_VerifiedById",
                table: "Nonconformities",
                column: "VerifiedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Nonconformities_KnowledgeArticles_ArticleId",
                table: "Nonconformities");

            migrationBuilder.DropForeignKey(
                name: "FK_Nonconformities_Users_VerifiedById",
                table: "Nonconformities");

            migrationBuilder.DropTable(
                name: "NonconformityAttachments");

            migrationBuilder.DropIndex(
                name: "IX_Nonconformities_ArticleId",
                table: "Nonconformities");

            migrationBuilder.DropIndex(
                name: "IX_Nonconformities_VerifiedById",
                table: "Nonconformities");

            migrationBuilder.DropColumn(
                name: "ArticleId",
                table: "Nonconformities");

            migrationBuilder.DropColumn(
                name: "VerificationNote",
                table: "Nonconformities");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "Nonconformities");

            migrationBuilder.DropColumn(
                name: "VerifiedById",
                table: "Nonconformities");
        }
    }
}
