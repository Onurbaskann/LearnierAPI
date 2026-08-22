using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnier.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SystemGeneratedPlanFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_system_generated",
                table: "subscription_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Demo satin alma bugune kadar plani "{Alan} {n}x{ay} {sure}dk Demo"
            // adiyla ortuk uretti; bu satirlar yonetici eliyle olusturulmadi ve
            // ogrenci kataloguna girmemeli.
            migrationBuilder.Sql(
                """
                UPDATE subscription_plans
                SET is_system_generated = TRUE
                WHERE name LIKE '%dk Demo';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_system_generated",
                table: "subscription_plans");
        }
    }
}
