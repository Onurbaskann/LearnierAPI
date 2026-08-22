using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnier.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingCreditLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_credit_ledger_booking_id",
                table: "credit_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_credit_ledger_quantity_not_zero",
                table: "credit_ledger");

            migrationBuilder.Sql(
                """
                UPDATE credit_ledger
                SET transaction_type = CASE transaction_type
                    WHEN 'BookingUsage' THEN 'Reserve'
                    WHEN 'CancellationRefund' THEN 'Refund'
                    WHEN 'Expiration' THEN 'Expire'
                    ELSE transaction_type
                END
                WHERE transaction_type IN ('BookingUsage', 'CancellationRefund', 'Expiration');
                """);

            migrationBuilder.CreateIndex(
                name: "ix_credit_ledger_booking_id_transaction_type",
                table: "credit_ledger",
                columns: new[] { "booking_id", "transaction_type" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_credit_ledger_quantity_not_zero",
                table: "credit_ledger",
                sql: "quantity <> 0 OR transaction_type = 'Consume'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_credit_ledger_booking_id_transaction_type",
                table: "credit_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_credit_ledger_quantity_not_zero",
                table: "credit_ledger");

            migrationBuilder.Sql("DELETE FROM credit_ledger WHERE transaction_type = 'Consume';");

            migrationBuilder.Sql(
                """
                UPDATE credit_ledger
                SET transaction_type = CASE transaction_type
                    WHEN 'Reserve' THEN 'BookingUsage'
                    WHEN 'Refund' THEN 'CancellationRefund'
                    WHEN 'Expire' THEN 'Expiration'
                    ELSE transaction_type
                END
                WHERE transaction_type IN ('Reserve', 'Refund', 'Expire');
                """);

            migrationBuilder.CreateIndex(
                name: "ix_credit_ledger_booking_id",
                table: "credit_ledger",
                column: "booking_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_credit_ledger_quantity_not_zero",
                table: "credit_ledger",
                sql: "quantity <> 0");
        }
    }
}
