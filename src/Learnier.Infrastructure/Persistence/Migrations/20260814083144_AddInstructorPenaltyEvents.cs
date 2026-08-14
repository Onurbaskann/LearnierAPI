using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnier.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260814083144_AddInstructorPenaltyEvents")]
public partial class AddInstructorPenaltyEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "pending_percentage", table: "instructor_penalty_states",
            type: "numeric(5,2)", precision: 5, scale: 2,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "instructor_penalty_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                instructor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: true),
                earning_id = table.Column<Guid>(type: "uuid", nullable: true),
                event_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                level = table.Column<int>(type: "integer", nullable: false),
                percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_instructor_penalty_events", x => x.id);
                table.CheckConstraint("ck_instructor_penalty_event_level", "level >= 0");
                table.CheckConstraint("ck_instructor_penalty_event_percentage", "percentage >= 0 AND percentage <= 100");
                table.ForeignKey("fk_instructor_penalty_events_instructor_earnings_earning_id", x => x.earning_id, "instructor_earnings", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_instructor_penalty_events_instructor_profiles_instructor_pr", x => x.instructor_profile_id, "instructor_profiles", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_instructor_penalty_events_lesson_sessions_session_id", x => x.session_id, "lesson_sessions", "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddCheckConstraint(
            name: "ck_instructor_penalty_state_percentage",
            table: "instructor_penalty_states",
            sql: "pending_percentage IS NULL OR pending_percentage BETWEEN 0 AND 100");
        migrationBuilder.CreateIndex("ix_instructor_penalty_events_earning_id", "instructor_penalty_events", "earning_id");
        migrationBuilder.CreateIndex("ix_instructor_penalty_events_instructor_profile_id_occurred_at", "instructor_penalty_events", new[] { "instructor_profile_id", "occurred_at" });
        migrationBuilder.CreateIndex("ix_instructor_penalty_events_instructor_profile_id_session_id_", "instructor_penalty_events", new[] { "instructor_profile_id", "session_id", "event_type" }, unique: true, filter: "session_id IS NOT NULL");
        migrationBuilder.CreateIndex("ix_instructor_penalty_events_session_id", "instructor_penalty_events", "session_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "instructor_penalty_events");
        migrationBuilder.DropCheckConstraint(name: "ck_instructor_penalty_state_percentage", table: "instructor_penalty_states");
        migrationBuilder.DropColumn(name: "pending_percentage", table: "instructor_penalty_states");
    }
}
