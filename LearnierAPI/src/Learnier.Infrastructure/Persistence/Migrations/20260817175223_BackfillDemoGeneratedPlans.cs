using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnier.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillDemoGeneratedPlans : Migration
    {
        /// <summary>
        /// Demo satin almanin urettigi eski planlari isaretler.
        /// </summary>
        /// <remarks>
        /// Onceki backfill plan adina bakiyordu ("%dk Demo"). Ad bicimi zaman icinde
        /// degistigi icin sure tasimayan eski satirlar isaretsiz kaldi ve ogrenci
        /// kataloguna sizdi. Aciklama metni ise akisin basindan beri sabit; kaynagi
        /// tanimanin daha guvenilir yolu bu.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE subscription_plans
                SET is_system_generated = TRUE
                WHERE is_system_generated = FALSE
                  AND description = 'Ödeme sağlayıcısı bağlanana kadar kullanılan kalıcı demo paketi.';
                """);
        }

        /// <summary>
        /// Geri alinamaz: bu satirlarin daha once hangi degeri tasidigi kayitli degil.
        /// Isaret yalnizca katalog gorunurlugunu etkiledigi icin geri alma bos birakildi.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
