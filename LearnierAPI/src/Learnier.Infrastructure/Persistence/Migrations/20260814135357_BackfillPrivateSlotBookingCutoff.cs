using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnier.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Rezervasyon artik ders baslangicina otuz dakika kala kapaniyor. Daha once
    /// acilmis birebir slotlarda kapanis ani baslangica esitti; bu veri duzeltmesi
    /// yalnizca gelecekteki ve iptal edilmemis slotlari yeni kurala tasir.
    /// </summary>
    public partial class BackfillPrivateSlotBookingCutoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE lesson_sessions
                SET booking_closes_at = starts_at - INTERVAL '30 minutes'
                WHERE session_type = 'Private'
                  AND status NOT IN ('Cancelled', 'Completed')
                  AND starts_at > NOW()
                  AND booking_closes_at IS NOT NULL
                  AND booking_closes_at > starts_at - INTERVAL '30 minutes';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE lesson_sessions
                SET booking_closes_at = starts_at
                WHERE session_type = 'Private'
                  AND status NOT IN ('Cancelled', 'Completed')
                  AND starts_at > NOW()
                  AND booking_closes_at = starts_at - INTERVAL '30 minutes';
                """);
        }
    }
}
