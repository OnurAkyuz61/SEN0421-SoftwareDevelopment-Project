using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class SurveyKpiAndQualityThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── KpiSnapshots: memnuniyet ölçümünün kendi kalitesi ──────────
            // CsatAverage tek başına yanıltıcıydı: iki yanıttan gelen 4,8 ile
            // iki yüz yanıttan gelen 4,8 panelde aynı görünüyordu. Üç kolon
            // eklenir (ISO 9001 md. 9.1.2):
            //   SurveyResponseCount : dönemde gelen değerlendirme sayısı
            //   CsatResponseRate    : çözülen taleplerin kaçı değerlendirildi (%)
            //   LowCsatCount        : eşik altı (uygunsuzluk doğuran) yanıt sayısı
            //
            // Kolonlar 0 varsayılanıyla gelir. GEÇMİŞ dönem kayıtları, yeniden
            // hesaplanana kadar 0 görünür: anket verisi SurveyResponses'ta
            // duruyor, kayıp yok. KpiAggregatorService yarım saatte bir bugünü,
            // dünü ve içinde bulunulan ayı yeniden hesaplar; daha eski dönemler
            // için KpiService.UpsertMonthlyAsync ilgili ay/yıl ile çağrılır.
            // Geriye dönük SQL ile doldurmak, dönem tanımını (talebin açılış
            // tarihi + birim filtresi) migration içinde ikinci kez yazmak
            // demekti; hesabın tek kaynağı KpiService olarak kalıyor.
            migrationBuilder.AddColumn<decimal>(
                name: "CsatResponseRate",
                table: "KpiSnapshots",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LowCsatCount",
                table: "KpiSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SurveyResponseCount",
                table: "KpiSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CsatResponseRate",
                table: "KpiSnapshots");

            migrationBuilder.DropColumn(
                name: "LowCsatCount",
                table: "KpiSnapshots");

            migrationBuilder.DropColumn(
                name: "SurveyResponseCount",
                table: "KpiSnapshots");
        }
    }
}
