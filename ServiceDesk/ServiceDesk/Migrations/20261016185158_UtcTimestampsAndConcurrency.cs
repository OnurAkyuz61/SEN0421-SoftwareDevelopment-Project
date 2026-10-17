using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class UtcTimestampsAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mevcut kayıtlar yerel saatle yazılmıştı; hepsi UTC'ye çevriliyor.
            // AT TIME ZONE kullanılıyor ki geçmişteki yaz saati uygulamaları da doğru hesaplansın.
            // (Users.CreatedAt zaten UTC olarak eklenmişti, ona dokunulmuyor.)
            migrationBuilder.Sql(@"
                UPDATE Tickets SET
                    CreatedAt         = CAST(CreatedAt         AT TIME ZONE 'Turkey Standard Time' AT TIME ZONE 'UTC' AS datetime2),
                    SlaDueAt          = CAST(SlaDueAt          AT TIME ZONE 'Turkey Standard Time' AT TIME ZONE 'UTC' AS datetime2),
                    ResolvedAt        = CAST(ResolvedAt        AT TIME ZONE 'Turkey Standard Time' AT TIME ZONE 'UTC' AS datetime2),
                    ApprovalDecidedAt = CAST(ApprovalDecidedAt AT TIME ZONE 'Turkey Standard Time' AT TIME ZONE 'UTC' AS datetime2);");

            migrationBuilder.Sql(@"
                UPDATE Feedbacks SET
                    CreatedAt = CAST(CreatedAt AT TIME ZONE 'Turkey Standard Time' AT TIME ZONE 'UTC' AS datetime2);");

            migrationBuilder.Sql(@"
                UPDATE Attachments SET
                    UploadedAt = CAST(UploadedAt AT TIME ZONE 'Turkey Standard Time' AT TIME ZONE 'UTC' AS datetime2);");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Tickets",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ApprovalStatus",
                table: "Tickets",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CreatedAt",
                table: "Tickets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status",
                table: "Tickets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status_SlaDueAt",
                table: "Tickets",
                columns: new[] { "Status", "SlaDueAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_ApprovalStatus",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_CreatedAt",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_Status",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_Status_SlaDueAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Tickets");

            // Tarihler yerel saate geri çevrilir
            migrationBuilder.Sql(@"
                UPDATE Tickets SET
                    CreatedAt         = CAST(CreatedAt         AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time' AS datetime2),
                    SlaDueAt          = CAST(SlaDueAt          AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time' AS datetime2),
                    ResolvedAt        = CAST(ResolvedAt        AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time' AS datetime2),
                    ApprovalDecidedAt = CAST(ApprovalDecidedAt AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time' AS datetime2);");

            migrationBuilder.Sql(@"
                UPDATE Feedbacks SET
                    CreatedAt = CAST(CreatedAt AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time' AS datetime2);");

            migrationBuilder.Sql(@"
                UPDATE Attachments SET
                    UploadedAt = CAST(UploadedAt AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time' AS datetime2);");
        }
    }
}
