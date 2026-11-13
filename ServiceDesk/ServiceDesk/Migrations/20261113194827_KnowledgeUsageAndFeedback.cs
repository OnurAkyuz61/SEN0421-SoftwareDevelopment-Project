using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <summary>
    /// Bilgi bankası makale kullanım ölçümü (ISO 9001 md. 9.1.2 / 9.1.3).
    ///
    /// ÜÇ İŞ, TEK GEÇİŞ:
    ///   1) KnowledgeArticleFeedbacks: okuyucu oyu ("faydalı / faydalı değil"),
    ///      ArticleId + UserId benzersiz — kullanıcı başına tek oy
    ///   2) KnowledgeArticles.HelpfulCount / NotHelpfulCount özet sayaçları
    ///   3) KnowledgeArticles.LastUsedAt — makalenin son kullanım anı
    ///
    /// VERİ TAŞIMA içerir: LastUsedAt, Tickets tablosundaki mevcut makale
    /// bağlantılarından geriye doldurulur (aşağıya bakın).
    /// </summary>
    public partial class KnowledgeUsageAndFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Özet oy sayaçları ─────────────────────────────────────────
            // Oyların kendisi KnowledgeArticleFeedbacks'te; bu iki kolon
            // denormalize özet. Yeni tablo boş doğduğu için ikisi de 0
            // başlar, taşınacak bir oy verisi yok.
            migrationBuilder.AddColumn<int>(
                name: "HelpfulCount",
                table: "KnowledgeArticles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NotHelpfulCount",
                table: "KnowledgeArticles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // ── Son kullanım anı ──────────────────────────────────────────
            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAt",
                table: "KnowledgeArticles",
                type: "datetime2",
                nullable: true);

            // ── Mevcut veriyi doldur ──────────────────────────────────────
            // UsedCount yıllardır tutuluyor ama "en son ne zaman" bilgisi
            // yoktu. Boş bırakılsa kullanılmayan makale raporu yanlış
            // okunurdu: sayacı dolu her makale "son kullanımı bilinmiyor"
            // görünür, hiçbiri de "eski" sayılmazdı.
            //
            // Tarih UYDURULMAZ, Tickets tablosundan okunur: makaleye bağlı
            // taleplerin en geç çözüm (yoksa kapanış, yoksa açılış) anı.
            // Çözülmemiş talebe makale bağlanmış olabileceği için üç tarih
            // sırayla denenir.
            //
            // Silinmiş (arşiv) talepler de sayılır: UsedCount da talep
            // silindiğinde azalmıyor, iki alan aynı olayı saymalı.
            //
            // Hiç talebe bağlanmamış makalede alan NULL kalır; rapor bunu
            // "eski" değil "hiç kullanılmadı" olarak değerlendirir
            // (KnowledgeUsagePolicy.IsStale / IsNeverUsed).
            migrationBuilder.Sql(@"
                UPDATE a
                   SET a.[LastUsedAt] = t.[SonKullanim]
                  FROM [KnowledgeArticles] a
                 INNER JOIN (
                        SELECT [ResolvedByArticleId] AS [ArticleId],
                               MAX(COALESCE([ResolvedAt], [ClosedAt], [CreatedAt])) AS [SonKullanim]
                          FROM [Tickets]
                         WHERE [ResolvedByArticleId] IS NOT NULL
                         GROUP BY [ResolvedByArticleId]
                       ) t ON t.[ArticleId] = a.[Id];");

            // ── Okuyucu oyu tablosu ───────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "KnowledgeArticleFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    IsHelpful = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeArticleFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeArticleFeedbacks_KnowledgeArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KnowledgeArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeArticleFeedbacks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Kullanıcı başına tek oy kuralı veritabanında duruyor: iki
            // sekmeden aynı anda gelen oy isteğinin ikincisi burada reddedilir
            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticleFeedbacks_ArticleId_UserId",
                table: "KnowledgeArticleFeedbacks",
                columns: new[] { "ArticleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticleFeedbacks_UserId",
                table: "KnowledgeArticleFeedbacks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Oylar geri alınamaz: eski şemada karşılığı olmayan yeni veri.
            // Geri dönülürse verilen bütün oylar silinir.
            migrationBuilder.DropTable(
                name: "KnowledgeArticleFeedbacks");

            migrationBuilder.DropColumn(
                name: "HelpfulCount",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "NotHelpfulCount",
                table: "KnowledgeArticles");

            // LastUsedAt de düşer; değeri Tickets tablosundan yeniden
            // türetilebildiği için kalıcı kayıp yok (yukarıdaki SQL).
            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "KnowledgeArticles");
        }
    }
}
