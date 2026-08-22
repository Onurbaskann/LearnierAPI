using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnier.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonPackageDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "lesson_duration_minutes",
                table: "subscription_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "monthly_lesson_credits",
                table: "subscription_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscription_plans_lesson_duration",
                table: "subscription_plans",
                sql: "lesson_duration_minutes IS NULL OR lesson_duration_minutes IN (30, 50)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscription_plans_lesson_package_complete",
                table: "subscription_plans",
                sql: "(monthly_lesson_credits IS NULL AND lesson_duration_minutes IS NULL) OR (monthly_lesson_credits IS NOT NULL AND lesson_duration_minutes IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscription_plans_monthly_credits_positive",
                table: "subscription_plans",
                sql: "monthly_lesson_credits IS NULL OR monthly_lesson_credits > 0");

            migrationBuilder.CreateIndex(
                name: "ix_plan_subject_access_plan_id",
                table: "plan_subject_access",
                column: "plan_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_subscription_plans_lesson_duration",
                table: "subscription_plans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscription_plans_lesson_package_complete",
                table: "subscription_plans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscription_plans_monthly_credits_positive",
                table: "subscription_plans");

            migrationBuilder.DropIndex(
                name: "ix_plan_subject_access_plan_id",
                table: "plan_subject_access");

            migrationBuilder.DropColumn(
                name: "lesson_duration_minutes",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "monthly_lesson_credits",
                table: "subscription_plans");
        }
    }
}
