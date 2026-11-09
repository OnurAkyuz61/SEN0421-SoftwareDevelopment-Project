using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class TicketWorkflowAndSlaAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Tickets ────────────────────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "Tickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelRequestedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstResponseBreachedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFirstResponseBreached",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MergedIntoTicketId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolutionCode",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootCause",
                table: "Tickets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaPausedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaPausedMinutes",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Tickets",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkMinutesTotal",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // ── Teams: round-robin ─────────────────────────────────────────
            migrationBuilder.AddColumn<int>(
                name: "LastAssignedUserId",
                table: "Teams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RoundRobinEnabled",
                table: "Teams",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // ── SlaPolicies: uyarı eşiği + sürüm ───────────────────────────
            migrationBuilder.AddColumn<int>(
                name: "RootPolicyId",
                table: "SlaPolicies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "SlaPolicies",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "WarningMinutesBefore",
                table: "SlaPolicies",
                type: "int",
                nullable: false,
                defaultValue: 120);

            // ── KpiSnapshots: ilk yanıt SLA'sı ayrı gösterge ───────────────
            migrationBuilder.AddColumn<int>(
                name: "FirstResponseBreachedTickets",
                table: "KpiSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FirstResponseComplianceRate",
                table: "KpiSnapshots",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: false,
                defaultValue: 0m);

            // ── Departments: birime özel mesai takvimi ─────────────────────
            migrationBuilder.AddColumn<int>(
                name: "BusinessCalendarId",
                table: "Departments",
                type: "int",
                nullable: true);

            // ── Yeni tablolar ──────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ChatbotQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AskCount = table.Column<int>(type: "int", nullable: false),
                    FirstAskedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAskedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArticleId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatbotQuestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketFollowers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketFollowers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketFollowers_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketFollowers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_BusinessCalendarId",
                table: "Departments",
                column: "BusinessCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketFollowers_TicketId_UserId",
                table: "TicketFollowers",
                columns: new[] { "TicketId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketFollowers_UserId",
                table: "TicketFollowers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_BusinessCalendars_BusinessCalendarId",
                table: "Departments",
                column: "BusinessCalendarId",
                principalTable: "BusinessCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── Mevcut veriyi doldur ───────────────────────────────────────
            // Yazışmalarda girilmiş harcanan süreler talep toplamına taşınır
            migrationBuilder.Sql(@"
UPDATE t SET WorkMinutesTotal = s.Toplam
FROM Tickets t
JOIN (SELECT TicketId, SUM(WorkMinutes) AS Toplam
      FROM TicketComments WHERE WorkMinutes IS NOT NULL
      GROUP BY TicketId) s ON s.TicketId = t.Id;");

            // Geçmişte ilk yanıt süresi aşılmış talepler ayrı ihlal olarak işaretlenir
            migrationBuilder.Sql(@"
UPDATE Tickets
SET IsFirstResponseBreached = 1,
    FirstResponseBreachedAt = CASE WHEN FirstRespondedAt IS NOT NULL THEN FirstRespondedAt ELSE FirstResponseDueAt END
WHERE FirstResponseDueAt IS NOT NULL
  AND ((FirstRespondedAt IS NOT NULL AND FirstRespondedAt > FirstResponseDueAt)
       OR (FirstRespondedAt IS NULL AND FirstResponseDueAt < SYSUTCDATETIME()));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_BusinessCalendars_BusinessCalendarId",
                table: "Departments");

            migrationBuilder.DropTable(
                name: "ChatbotQuestions");

            migrationBuilder.DropTable(
                name: "TicketFollowers");

            migrationBuilder.DropIndex(
                name: "IX_Departments_BusinessCalendarId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CancelRequestedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FirstResponseBreachedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IsFirstResponseBreached",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "MergedIntoTicketId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResolutionCode",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RootCause",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaPausedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaPausedMinutes",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "WorkMinutesTotal",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "LastAssignedUserId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "RoundRobinEnabled",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "RootPolicyId",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "WarningMinutesBefore",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "FirstResponseBreachedTickets",
                table: "KpiSnapshots");

            migrationBuilder.DropColumn(
                name: "FirstResponseComplianceRate",
                table: "KpiSnapshots");

            migrationBuilder.DropColumn(
                name: "BusinessCalendarId",
                table: "Departments");
        }
    }
}
