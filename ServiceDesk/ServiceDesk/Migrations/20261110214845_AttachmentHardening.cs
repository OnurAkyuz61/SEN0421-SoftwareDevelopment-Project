using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Attachments: yumuşak silme + antivirüs taraması ────────────
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Attachments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletedById",
                table: "Attachments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Attachments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ScanStatus",
                table: "Attachments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScannedAt",
                table: "Attachments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_DeletedById",
                table: "Attachments",
                column: "DeletedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_Users_DeletedById",
                table: "Attachments",
                column: "DeletedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── Mevcut veriyi doldur ───────────────────────────────────────
            // ScanStatus'un varsayılanı Pending (0) ve Pending dosya servis
            // edilmiyor. Bu geçişten ÖNCE yüklenmiş ekler hiç taranmadı; onları
            // Pending bırakmak tüm eski dosyaları bir anda indirilemez yapardı.
            // Bu yüzden geriye dönük "tarama kapsamı dışı" (Skipped = 4)
            // işaretlenirler: davranış eskisiyle aynı kalır, yeni yüklenen her
            // dosya ise gerçekten taramadan geçer.
            migrationBuilder.Sql(@"
                UPDATE [Attachments]
                   SET [ScanStatus] = 4,
                       [ScannedAt]  = [UploadedAt]
                 WHERE [ScanStatus] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_Users_DeletedById",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_DeletedById",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "ScanStatus",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "ScannedAt",
                table: "Attachments");
        }
    }
}
