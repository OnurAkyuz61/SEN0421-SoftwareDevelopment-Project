using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class SurveyLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Tickets: memnuniyet anketi penceresi ───────────────────────
            // SurveyInvitedAt     : anket davetinin açıldığı an (çözüm).
            //                       Doldurma süresi (Survey:ExpiresAfterDays)
            //                       ve hatırlatma gecikmesi buradan sayılır.
            // SurveyReminderSentAt: hatırlatma e-postasının gittiği an; dolu
            //                       olan talebe ikinci hatırlatma gönderilmez
            //                       (SurveyReminderService).
            migrationBuilder.AddColumn<DateTime>(
                name: "SurveyInvitedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SurveyReminderSentAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            // ── Mevcut veriyi doldur ───────────────────────────────────────
            // Bu geçişten önce çözülmüş talepler için davet anı kayıtlı değil.
            // Boş bırakılırsa anket süresi hiç başlamamış sayılır (TTL
            // hesaplanamaz) ve geçmişteki her çözülmüş talep bir anda
            // "hatırlatma bekliyor" durumuna düşerdi: uygulama ilk açılışında
            // yıllar öncesine ait taleplere e-posta gönderirdi.
            // Davet anı çözüm tarihine eşitlenir; süresi çoktan dolmuş eski
            // talepler böylece hatırlatma kapsamına da girmez.
            migrationBuilder.Sql(@"
                UPDATE [Tickets]
                   SET [SurveyInvitedAt] = [ResolvedAt]
                 WHERE [ResolvedAt] IS NOT NULL
                   AND [SurveyInvitedAt] IS NULL;");

            // Anketi ZATEN doldurulmuş talepler hatırlatma sorgusuna hiç
            // girmez (Survey != null koşulu), yine de işaretlenirler:
            // yapılandırma değişse ya da sorgu ileride gevşese bile
            // doldurulmuş ankete hatırlatma gitmesin.
            migrationBuilder.Sql(@"
                UPDATE [Tickets]
                   SET [SurveyReminderSentAt] = [ResolvedAt]
                 WHERE [ResolvedAt] IS NOT NULL
                   AND [Id] IN (SELECT [TicketId] FROM [SurveyResponses]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SurveyInvitedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SurveyReminderSentAt",
                table: "Tickets");
        }
    }
}
