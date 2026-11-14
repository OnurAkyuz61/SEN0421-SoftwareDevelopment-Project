using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <summary>
    /// Bilgi bankası yayın yönetişimi (ISO 9001 md. 7.5.2 / 7.5.3.2).
    ///
    /// İKİ İŞ, TEK GEÇİŞ:
    ///   1) IsPublished bayrağı yerine KnowledgeArticles.Status + onay izi
    ///      (SubmittedAt/SubmittedById, ApprovedAt/ApprovedById, RejectionNote)
    ///   2) KnowledgeArticleRevisions: makalenin sürüm geçmişi
    ///
    /// VERİ TAŞIMA içerir; adımların SIRASI önemlidir. Şema değişiklikleri
    /// EF tarafından üretildi, sıralama ve SQL blokları elle düzenlendi:
    /// üretilen hali IsPublished'ı Status eklenmeden ÖNCE düşürüyordu, yani
    /// taşınacak veri okunmadan silinmiş olurdu.
    /// </summary>
    public partial class KnowledgePublishingAndRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1) Yeni kolonlar (IsPublished henüz DURUYOR) ───────────────
            // Status varsayılanı 0 = Taslak. Mevcut satırlar bu değerle
            // doğar ve hemen aşağıdaki taşıma ile gerçek durumlarına
            // çekilir; sıra bozulursa her makale taslağa düşer ve bilgi
            // bankası son kullanıcı için bir anda boşalır.
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "KnowledgeArticles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "KnowledgeArticles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubmittedById",
                table: "KnowledgeArticles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "KnowledgeArticles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedById",
                table: "KnowledgeArticles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionNote",
                table: "KnowledgeArticles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // ── 2) IsPublished → Status ────────────────────────────────────
            // Yayındaki makale yayında kalır (2 = Published): bu geçiş
            // onay süreci getiriyor diye kurumun hâlihazırda yayımlanmış
            // prosedürleri son kullanıcıdan gizlenmez.
            //
            // IsPublished = 0 olan kayıt, destek ekibinin bilinçli olarak
            // yayımlamadığı taslaktır ve taslak (0 = Draft) olarak kalır:
            // gözden geçirilmemiş metin bu geçişle yayına ÇIKARILMAZ.
            // Uygulamada IsPublished varsayılanı true olduğu için pratikte
            // bu satırların tamamı yayına taşınır.
            //
            // Onay izi (SubmittedAt / ApprovedAt / ApprovedById) bilerek
            // BOŞ bırakılır: bu makaleler onay süreci var olmadan önce
            // yayımlandı, gerçekte onaylayan kimse yok. Alanı yazarla
            // doldurmak denetim izine gerçekte yaşanmamış bir onay yazmak
            // olurdu (md. 7.5.2 kaydın doğru olmasını ister). Arayüz boş
            // onay kaydını "-" olarak gösterir.
            migrationBuilder.Sql(@"
                UPDATE [KnowledgeArticles]
                   SET [Status] = CASE WHEN [IsPublished] = 1 THEN 2 ELSE 0 END;");

            // Taşıma bittikten sonra eski bayrak düşer
            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "KnowledgeArticles");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_Status",
                table: "KnowledgeArticles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_SubmittedById",
                table: "KnowledgeArticles",
                column: "SubmittedById");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_ApprovedById",
                table: "KnowledgeArticles",
                column: "ApprovedById");

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeArticles_Users_SubmittedById",
                table: "KnowledgeArticles",
                column: "SubmittedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeArticles_Users_ApprovedById",
                table: "KnowledgeArticles",
                column: "ApprovedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── 3) Sürüm geçmişi tablosu ──────────────────────────────────
            migrationBuilder.CreateTable(
                name: "KnowledgeArticleRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeArticleRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeArticleRevisions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeArticleRevisions_KnowledgeArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KnowledgeArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeArticleRevisions_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticleRevisions_ArticleId_VersionNo",
                table: "KnowledgeArticleRevisions",
                columns: new[] { "ArticleId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticleRevisions_CategoryId",
                table: "KnowledgeArticleRevisions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticleRevisions_CreatedById",
                table: "KnowledgeArticleRevisions",
                column: "CreatedById");

            // ── 4) Mevcut makalelerin 1. sürümü ───────────────────────────
            // Geçmiş boş bırakılırsa var olan makalelerin sürüm listesi boş
            // görünür ve "bir sürüme geri dön" ilk kaydetmeye kadar hiçbir
            // şey yapamaz. Her makalenin BUGÜNKÜ hali 1. sürüm olarak
            // yazılır; metni yazan gerçekten yazardır (uydurma kayıt yok),
            // tarih ise içeriğin oluştuğu an: varsa son güncelleme, yoksa
            // oluşturma tarihi.
            migrationBuilder.Sql(@"
                INSERT INTO [KnowledgeArticleRevisions]
                       ([ArticleId], [VersionNo], [Title], [Body], [CategoryId],
                        [CreatedById], [CreatedAt], [Note])
                SELECT [Id], 1, [Title], [Body], [CategoryId],
                       [AuthorId], COALESCE([UpdatedAt], [CreatedAt]),
                       N'Geçiş öncesi mevcut sürüm'
                  FROM [KnowledgeArticles];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sürüm geçmişi geri alınamaz: bu tablodaki veri yeni, eski
            // şemada karşılığı yok. Geri dönülürse geçmiş silinir.
            migrationBuilder.DropTable(
                name: "KnowledgeArticleRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeArticles_Users_ApprovedById",
                table: "KnowledgeArticles");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeArticles_Users_SubmittedById",
                table: "KnowledgeArticles");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeArticles_ApprovedById",
                table: "KnowledgeArticles");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeArticles_Status",
                table: "KnowledgeArticles");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeArticles_SubmittedById",
                table: "KnowledgeArticles");

            // Bayrak önce geri gelir, sonra Status'tan DOLDURULUR, en son
            // Status düşer. Ters sırada yazılırsa (EF'in ürettiği gibi)
            // bütün makaleler IsPublished = 0 ile kalır ve bilgi bankası
            // geri dönüşte boşalır.
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "KnowledgeArticles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Yalnızca "Yayında" olan makale yayımlanmış sayılır; onay
            // bekleyen ve reddedilen makale eski şemada taslaktır.
            migrationBuilder.Sql(@"
                UPDATE [KnowledgeArticles]
                   SET [IsPublished] = CASE WHEN [Status] = 2 THEN 1 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "SubmittedById",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "RejectionNote",
                table: "KnowledgeArticles");
        }
    }
}
