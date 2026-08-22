using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnier.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyCreditRenewal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "period_start",
                table: "credit_ledger",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_credit_ledger_due_period_grants",
                table: "credit_ledger",
                columns: new[] { "expires_at", "subscription_id" },
                filter: "transaction_type = 'PeriodGrant'");

            migrationBuilder.CreateIndex(
                name: "ix_credit_ledger_subscription_id_session_type_transaction_type",
                table: "credit_ledger",
                columns: new[] { "subscription_id", "session_type", "transaction_type", "period_start" },
                unique: true,
                filter: "period_start IS NOT NULL AND transaction_type IN ('PeriodGrant', 'Expire')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_credit_ledger_due_period_grants",
                table: "credit_ledger");

            migrationBuilder.DropIndex(
                name: "ix_credit_ledger_subscription_id_session_type_transaction_type",
                table: "credit_ledger");

            migrationBuilder.DropColumn(
                name: "period_start",
                table: "credit_ledger");
        }
    }
}
