using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnier.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260814080843_AddCancellationPolicy")]
public partial class AddCancellationPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "cancellation_policy_version",
            table: "lesson_sessions",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "instructor_cancellation_deadline_at",
            table: "lesson_sessions",
            type: "timestamptz",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "cancellation_policies",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                student_refund_cutoff_minutes = table.Column<int>(type: "integer", nullable: false),
                instructor_penalty_cutoff_minutes = table.Column<int>(type: "integer", nullable: false),
                version = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_cancellation_policies", x => x.id);
                table.CheckConstraint("ck_cancellation_policy_instructor_cutoff", "instructor_penalty_cutoff_minutes BETWEEN 0 AND 10080");
                table.CheckConstraint("ck_cancellation_policy_student_cutoff", "student_refund_cutoff_minutes BETWEEN 0 AND 10080");
                table.CheckConstraint("ck_cancellation_policy_version", "version > 0");
                table.ForeignKey(
                    name: "fk_cancellation_policies_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_cancellation_policies_organization_id",
            table: "cancellation_policies",
            column: "organization_id",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "cancellation_policies");
        migrationBuilder.DropColumn(name: "cancellation_policy_version", table: "lesson_sessions");
        migrationBuilder.DropColumn(name: "instructor_cancellation_deadline_at", table: "lesson_sessions");
    }
}
