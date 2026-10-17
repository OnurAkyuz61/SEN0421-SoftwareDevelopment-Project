using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ServiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class SeedSplitDemoUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Demo kullanıcılar migration seed'inden (HasData) çıkarıldı; artık
            // yalnızca Development ortamında DevDataSeeder oluşturuyor.
            //
            // EF varsayılan olarak bu satırları SİLMEK ister. Silinmemeleri gerekir:
            //   1) Mevcut talep, yorum ve geçmiş kayıtları bu kullanıcılara bağlı;
            //      silme yabancı anahtar kısıtını ihlal ederdi.
            //   2) Amaç hesapları yok etmek değil, canlı veritabanına seed yoluyla
            //      bilinen şifreli hesap yazılmasını durdurmak.
            //
            // Bu yüzden Up bilinçli olarak boştur: mevcut veritabanları hesaplarını
            // korur, sıfırdan kurulan canlı veritabanına ise hiç hesap yazılmaz.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alınacak bir şey yok; bu migration veri değiştirmiyor.
        }
    }
}
