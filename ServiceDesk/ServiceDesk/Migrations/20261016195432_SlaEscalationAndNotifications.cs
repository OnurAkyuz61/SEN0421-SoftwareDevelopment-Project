using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class SlaEscalationAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_Priority",
                table: "SlaPolicies");

            migrationBuilder.RenameColumn(
                name: "ResolutionHours",
                table: "SlaPolicies",
                newName: "ResolutionMinutes");

            // Kolon yeniden adlandırıldı ama içindeki değerler hâlâ saat cinsinden.
            // Seed satırları aşağıda zaten mutlak değerlerle güncelleniyor; bu dönüşüm
            // elle eklenmiş politikaların da doğru taşınmasını sağlar.
            migrationBuilder.Sql("UPDATE SlaPolicies SET ResolutionMinutes = ResolutionMinutes * 60;");

            migrationBuilder.AddColumn<int>(
                name: "EscalationLevel",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstRespondedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstResponseDueAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSlaBreached",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaBreachedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaPolicyId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaWarningSentAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppliesToType",
                table: "SlaPolicies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BusinessCalendarId",
                table: "SlaPolicies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BusinessHoursOnly",
                table: "SlaPolicies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveFrom",
                table: "SlaPolicies",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveTo",
                table: "SlaPolicies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirstResponseMinutes",
                table: "SlaPolicies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "SlaPolicies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "SlaPolicies",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BusinessCalendars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkStartMinutes = table.Column<int>(type: "int", nullable: false),
                    WorkEndMinutes = table.Column<int>(type: "int", nullable: false),
                    WorkDays = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EscalationRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MinutesOverdue = table.Column<int>(type: "int", nullable: false),
                    EscalatedToId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscalationRecords_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EscalationRecords_Users_EscalatedToId",
                        column: x => x.EscalatedToId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EscalationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Level = table.Column<int>(type: "int", nullable: false),
                    AfterMinutes = table.Column<int>(type: "int", nullable: false),
                    TargetRole = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipientId = table.Column<int>(type: "int", nullable: false),
                    TicketId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsSent = table.Column<bool>(type: "bit", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SendError = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notifications_Users_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessCalendarId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holidays_BusinessCalendars_BusinessCalendarId",
                        column: x => x.BusinessCalendarId,
                        principalTable: "BusinessCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "BusinessCalendars",
                columns: new[] { "Id", "Name", "WorkDays", "WorkEndMinutes", "WorkStartMinutes" },
                values: new object[] { 1, "Varsayılan Mesai", "1,2,3,4,5", 1080, 540 });

            migrationBuilder.InsertData(
                table: "EscalationRules",
                columns: new[] { "Id", "AfterMinutes", "IsActive", "Level", "TargetRole" },
                values: new object[,]
                {
                    { 1, 0, true, 1, 1 },
                    { 2, 120, true, 2, 2 }
                });

            migrationBuilder.UpdateData(
                table: "SlaPolicies",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AppliesToType", "BusinessCalendarId", "BusinessHoursOnly", "EffectiveFrom", "EffectiveTo", "FirstResponseMinutes", "IsActive", "Name", "ResolutionMinutes" },
                values: new object[] { null, null, false, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 30, true, "Kritik", 240 });

            migrationBuilder.UpdateData(
                table: "SlaPolicies",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "AppliesToType", "BusinessCalendarId", "BusinessHoursOnly", "EffectiveFrom", "EffectiveTo", "FirstResponseMinutes", "IsActive", "Name", "ResolutionMinutes" },
                values: new object[] { null, 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 60, true, "Yüksek", 480 });

            migrationBuilder.UpdateData(
                table: "SlaPolicies",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "AppliesToType", "BusinessCalendarId", "BusinessHoursOnly", "EffectiveFrom", "EffectiveTo", "FirstResponseMinutes", "IsActive", "Name", "ResolutionMinutes" },
                values: new object[] { null, 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 240, true, "Orta", 1440 });

            migrationBuilder.UpdateData(
                table: "SlaPolicies",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "AppliesToType", "BusinessCalendarId", "BusinessHoursOnly", "EffectiveFrom", "EffectiveTo", "FirstResponseMinutes", "IsActive", "Name", "ResolutionMinutes" },
                values: new object[] { null, 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 480, true, "Düşük", 4320 });

            migrationBuilder.InsertData(
                table: "Holidays",
                columns: new[] { "Id", "BusinessCalendarId", "Date", "Name" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Yılbaşı" },
                    { 2, 1, new DateTime(2026, 4, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), "Ulusal Egemenlik ve Çocuk Bayramı" },
                    { 3, 1, new DateTime(2026, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Emek ve Dayanışma Günü" },
                    { 4, 1, new DateTime(2026, 5, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), "Atatürk'ü Anma, Gençlik ve Spor Bayramı" },
                    { 5, 1, new DateTime(2026, 7, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "Demokrasi ve Millî Birlik Günü" },
                    { 6, 1, new DateTime(2026, 8, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), "Zafer Bayramı" },
                    { 7, 1, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cumhuriyet Bayramı" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SlaPolicyId",
                table: "Tickets",
                column: "SlaPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_BusinessCalendarId",
                table: "SlaPolicies",
                column: "BusinessCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Priority_IsActive",
                table: "SlaPolicies",
                columns: new[] { "Priority", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRecords_EscalatedToId",
                table: "EscalationRecords",
                column: "EscalatedToId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRecords_TicketId",
                table: "EscalationRecords",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_Level",
                table: "EscalationRules",
                column: "Level",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_BusinessCalendarId_Date",
                table: "Holidays",
                columns: new[] { "BusinessCalendarId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId_ReadAt_CreatedAt",
                table: "Notifications",
                columns: new[] { "RecipientId", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TicketId",
                table: "Notifications",
                column: "TicketId");

            migrationBuilder.AddForeignKey(
                name: "FK_SlaPolicies_BusinessCalendars_BusinessCalendarId",
                table: "SlaPolicies",
                column: "BusinessCalendarId",
                principalTable: "BusinessCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_SlaPolicies_SlaPolicyId",
                table: "Tickets",
                column: "SlaPolicyId",
                principalTable: "SlaPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SlaPolicies_BusinessCalendars_BusinessCalendarId",
                table: "SlaPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_SlaPolicies_SlaPolicyId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "EscalationRecords");

            migrationBuilder.DropTable(
                name: "EscalationRules");

            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "BusinessCalendars");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_SlaPolicyId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_BusinessCalendarId",
                table: "SlaPolicies");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_Priority_IsActive",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "EscalationLevel",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FirstRespondedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FirstResponseDueAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IsSlaBreached",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaBreachedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaPolicyId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaWarningSentAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AppliesToType",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "BusinessCalendarId",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "BusinessHoursOnly",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "FirstResponseMinutes",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "SlaPolicies");

            migrationBuilder.RenameColumn(
                name: "ResolutionMinutes",
                table: "SlaPolicies",
                newName: "ResolutionHours");

            migrationBuilder.UpdateData(
                table: "SlaPolicies",
                keyColumn: "Id",
                keyValue: 1,
                column: "ResolutionHours",
                value: 4);

            migrationBuilder.UpdateData(
                table: "SlaPolicies",
                keyColumn: "Id",
                keyValue: 2,
                column: "ResolutionHours",
                value: 8);

            migrationBuilder.UpdateData(
                table: "SlaPolicies",
                keyColumn: "Id",
                keyValue: 3,
                column: "ResolutionHours",
                value: 24);

            migrationBuilder.UpdateData(
                table: "SlaPolicies",
                keyColumn: "Id",
                keyValue: 4,
                column: "ResolutionHours",
                value: 72);

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Priority",
                table: "SlaPolicies",
                column: "Priority",
                unique: true);
        }
    }
}
