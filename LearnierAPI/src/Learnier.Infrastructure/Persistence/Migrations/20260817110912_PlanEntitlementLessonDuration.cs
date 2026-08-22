using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnier.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanEntitlementLessonDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_plan_entitlements_plan_id_entitlement_type_session_type",
                table: "plan_entitlements");

            migrationBuilder.AddColumn<int>(
                name: "lesson_duration_minutes",
                table: "plan_entitlements",
                type: "integer",
                nullable: true);

            // Mevcut birebir ders kredileri planin denormalize suresinden doldurulur;
            // aksi halde asagidaki check constraint eski satirlarda patlar. Suresi
            // olmayan eski planlarda 50 dakika varsayilir - EfActivePackageQueries
            // bugune kadar ayni varsayimla goruntuluyordu.
            migrationBuilder.Sql(
                """
                UPDATE plan_entitlements AS e
                SET lesson_duration_minutes = COALESCE(p.lesson_duration_minutes, 50)
                FROM subscription_plans AS p
                WHERE e.plan_id = p.id
                  AND e.entitlement_type = 'LessonCredit'
                  AND e.session_type = 'Private';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_plan_entitlements_plan_id_entitlement_type_session_type_les",
                table: "plan_entitlements",
                columns: new[] { "plan_id", "entitlement_type", "session_type", "lesson_duration_minutes" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_plan_entitlements_lesson_duration",
                table: "plan_entitlements",
                sql: "lesson_duration_minutes IS NULL OR lesson_duration_minutes IN (30, 50)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plan_entitlements_private_credit_duration",
                table: "plan_entitlements",
                sql: "(entitlement_type = 'LessonCredit' AND session_type = 'Private') = (lesson_duration_minutes IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_plan_entitlements_plan_id_entitlement_type_session_type_les",
                table: "plan_entitlements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plan_entitlements_lesson_duration",
                table: "plan_entitlements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plan_entitlements_private_credit_duration",
                table: "plan_entitlements");

            migrationBuilder.DropColumn(
                name: "lesson_duration_minutes",
                table: "plan_entitlements");

            migrationBuilder.CreateIndex(
                name: "ix_plan_entitlements_plan_id_entitlement_type_session_type",
                table: "plan_entitlements",
                columns: new[] { "plan_id", "entitlement_type", "session_type" },
                unique: true);
        }
    }
}
