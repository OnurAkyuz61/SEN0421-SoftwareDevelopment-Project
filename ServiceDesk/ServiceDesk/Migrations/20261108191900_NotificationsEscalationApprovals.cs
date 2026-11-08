using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class NotificationsEscalationApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChangeApprovals_TicketId_Step_ApproverId",
                table: "ChangeApprovals");

            migrationBuilder.AddColumn<bool>(
                name: "DailyDigestEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDigestSentAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalRound",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStepsRequired",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastApprovalReminderAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFailed",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "Notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "Notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FallbackRole",
                table: "EscalationRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FallbackUserId",
                table: "EscalationRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RaisePriority",
                table: "EscalationRules",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetType",
                table: "EscalationRules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsManual",
                table: "EscalationRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PriorityAfter",
                table: "EscalationRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriorityBefore",
                table: "EscalationRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TriggeredById",
                table: "EscalationRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsedFallback",
                table: "EscalationRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CabMeetingAt",
                table: "ChangeDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CabMeetingUrl",
                table: "ChangeDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImplementationNote",
                table: "ChangeDetails",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImplementationStartedAt",
                table: "ChangeDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImplementationStatus",
                table: "ChangeDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImplementedAt",
                table: "ChangeDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RolledBackAt",
                table: "ChangeDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OnBehalfOfId",
                table: "ChangeApprovals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "ChangeApprovals",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ApprovalChainRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    MinPriority = table.Column<int>(type: "int", nullable: true),
                    Risk = table.Column<int>(type: "int", nullable: true),
                    RequiredSteps = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalChainRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalChainRules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalDelegations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DelegatorId = table.Column<int>(type: "int", nullable: false),
                    DelegateId = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDelegations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_Users_DelegateId",
                        column: x => x.DelegateId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_Users_DelegatorId",
                        column: x => x.DelegatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TicketId = table.Column<int>(type: "int", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    InApp = table.Column<bool>(type: "bit", nullable: false),
                    Email = table.Column<bool>(type: "bit", nullable: false),
                    Sms = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OtpDeliveryLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    MaskedPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    SendNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatusUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpDeliveryLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtpDeliveryLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                table: "EscalationRules",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FallbackRole", "FallbackUserId", "RaisePriority", "TargetType" },
                values: new object[] { null, null, true, 0 });

            migrationBuilder.UpdateData(
                table: "EscalationRules",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "FallbackRole", "FallbackUserId", "RaisePriority", "TargetType" },
                values: new object[] { null, null, true, 0 });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Channel_IsSent_IsFailed_NextAttemptAt",
                table: "Notifications",
                columns: new[] { "Channel", "IsSent", "IsFailed", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_FallbackUserId",
                table: "EscalationRules",
                column: "FallbackUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRecords_OccurredAt",
                table: "EscalationRecords",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRecords_TriggeredById",
                table: "EscalationRecords",
                column: "TriggeredById");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_OnBehalfOfId",
                table: "ChangeApprovals",
                column: "OnBehalfOfId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_TicketId_Round_Step_ApproverId",
                table: "ChangeApprovals",
                columns: new[] { "TicketId", "Round", "Step", "ApproverId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalChainRules_CategoryId",
                table: "ApprovalChainRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_DelegateId_StartsAt_EndsAt",
                table: "ApprovalDelegations",
                columns: new[] { "DelegateId", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_DelegatorId",
                table: "ApprovalDelegations",
                column: "DelegatorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId_Type",
                table: "NotificationPreferences",
                columns: new[] { "UserId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtpDeliveryLogs_ProviderMessageId",
                table: "OtpDeliveryLogs",
                column: "ProviderMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_OtpDeliveryLogs_UserId_CreatedAt",
                table: "OtpDeliveryLogs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChangeApprovals_Users_OnBehalfOfId",
                table: "ChangeApprovals",
                column: "OnBehalfOfId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscalationRecords_Users_TriggeredById",
                table: "EscalationRecords",
                column: "TriggeredById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscalationRules_Users_FallbackUserId",
                table: "EscalationRules",
                column: "FallbackUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChangeApprovals_Users_OnBehalfOfId",
                table: "ChangeApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_EscalationRecords_Users_TriggeredById",
                table: "EscalationRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_EscalationRules_Users_FallbackUserId",
                table: "EscalationRules");

            migrationBuilder.DropTable(
                name: "ApprovalChainRules");

            migrationBuilder.DropTable(
                name: "ApprovalDelegations");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "OtpDeliveryLogs");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_Channel_IsSent_IsFailed_NextAttemptAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_EscalationRules_FallbackUserId",
                table: "EscalationRules");

            migrationBuilder.DropIndex(
                name: "IX_EscalationRecords_OccurredAt",
                table: "EscalationRecords");

            migrationBuilder.DropIndex(
                name: "IX_EscalationRecords_TriggeredById",
                table: "EscalationRecords");

            migrationBuilder.DropIndex(
                name: "IX_ChangeApprovals_OnBehalfOfId",
                table: "ChangeApprovals");

            migrationBuilder.DropIndex(
                name: "IX_ChangeApprovals_TicketId_Round_Step_ApproverId",
                table: "ChangeApprovals");

            migrationBuilder.DropColumn(
                name: "DailyDigestEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastDigestSentAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApprovalRound",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ApprovalStepsRequired",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "LastApprovalReminderAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsFailed",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "FallbackRole",
                table: "EscalationRules");

            migrationBuilder.DropColumn(
                name: "FallbackUserId",
                table: "EscalationRules");

            migrationBuilder.DropColumn(
                name: "RaisePriority",
                table: "EscalationRules");

            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "EscalationRules");

            migrationBuilder.DropColumn(
                name: "IsManual",
                table: "EscalationRecords");

            migrationBuilder.DropColumn(
                name: "PriorityAfter",
                table: "EscalationRecords");

            migrationBuilder.DropColumn(
                name: "PriorityBefore",
                table: "EscalationRecords");

            migrationBuilder.DropColumn(
                name: "TriggeredById",
                table: "EscalationRecords");

            migrationBuilder.DropColumn(
                name: "UsedFallback",
                table: "EscalationRecords");

            migrationBuilder.DropColumn(
                name: "CabMeetingAt",
                table: "ChangeDetails");

            migrationBuilder.DropColumn(
                name: "CabMeetingUrl",
                table: "ChangeDetails");

            migrationBuilder.DropColumn(
                name: "ImplementationNote",
                table: "ChangeDetails");

            migrationBuilder.DropColumn(
                name: "ImplementationStartedAt",
                table: "ChangeDetails");

            migrationBuilder.DropColumn(
                name: "ImplementationStatus",
                table: "ChangeDetails");

            migrationBuilder.DropColumn(
                name: "ImplementedAt",
                table: "ChangeDetails");

            migrationBuilder.DropColumn(
                name: "RolledBackAt",
                table: "ChangeDetails");

            migrationBuilder.DropColumn(
                name: "OnBehalfOfId",
                table: "ChangeApprovals");

            migrationBuilder.DropColumn(
                name: "Round",
                table: "ChangeApprovals");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_TicketId_Step_ApproverId",
                table: "ChangeApprovals",
                columns: new[] { "TicketId", "Step", "ApproverId" },
                unique: true);
        }
    }
}
