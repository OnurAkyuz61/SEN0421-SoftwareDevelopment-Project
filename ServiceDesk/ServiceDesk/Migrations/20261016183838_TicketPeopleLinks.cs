using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class TicketPeopleLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_RequesterId",
                table: "Tickets");

            // Yeni kolon, eski metin alanı silinmeden önce eklenir ki atamalar taşınabilsin
            migrationBuilder.AddColumn<int>(
                name: "AssigneeId",
                table: "Tickets",
                type: "int",
                nullable: true);

            // Atanan kişinin adını kullanıcı kaydıyla eşleştir
            migrationBuilder.Sql(@"
                UPDATE t
                   SET t.AssigneeId = u.Id
                  FROM Tickets t
                  JOIN Users u ON LTRIM(RTRIM(u.FirstName + ' ' + u.LastName)) = LTRIM(RTRIM(t.AssignedTo))
                 WHERE t.AssignedTo IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "AssignedTo",
                table: "Tickets");

            // Talep edeni boş olan eski kayıtları ad soyaddan eşleştir
            migrationBuilder.Sql(@"
                UPDATE t
                   SET t.RequesterId = u.Id
                  FROM Tickets t
                  JOIN Users u ON LTRIM(RTRIM(u.FirstName + ' ' + u.LastName)) = LTRIM(RTRIM(t.RequesterName))
                 WHERE t.RequesterId IS NULL;");

            // Eşleşmeyenler demo son kullanıcıya bağlanır; aksi halde NOT NULL kısıtı geçmez
            migrationBuilder.Sql(@"
                UPDATE Tickets
                   SET RequesterId = COALESCE(
                           (SELECT TOP 1 Id FROM Users WHERE Email = 'user@servicedesk.local'),
                           (SELECT MIN(Id) FROM Users))
                 WHERE RequesterId IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "RequesterId",
                table: "Tickets",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssigneeId",
                table: "Tickets",
                column: "AssigneeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_AssigneeId",
                table: "Tickets",
                column: "AssigneeId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_RequesterId",
                table: "Tickets",
                column: "RequesterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_AssigneeId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_RequesterId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_AssigneeId",
                table: "Tickets");

            // Metin alanı geri gelir ve atanan kişinin adı yeniden yazılır
            migrationBuilder.AddColumn<string>(
                name: "AssignedTo",
                table: "Tickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE t
                   SET t.AssignedTo = LTRIM(RTRIM(u.FirstName + ' ' + u.LastName))
                  FROM Tickets t
                  JOIN Users u ON u.Id = t.AssigneeId;");

            migrationBuilder.DropColumn(
                name: "AssigneeId",
                table: "Tickets");

            migrationBuilder.AlterColumn<int>(
                name: "RequesterId",
                table: "Tickets",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_RequesterId",
                table: "Tickets",
                column: "RequesterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
