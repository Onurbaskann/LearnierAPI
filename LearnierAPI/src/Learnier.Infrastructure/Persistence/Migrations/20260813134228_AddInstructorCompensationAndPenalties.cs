using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnier.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstructorCompensationAndPenalties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instructor_compensation_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instructor_compensation_rates", x => x.id);
                    table.CheckConstraint("ck_instructor_compensation_amount", "amount >= 0");
                    table.CheckConstraint("ck_instructor_compensation_duration", "lesson_duration_minutes IN (30, 50)");
                    table.ForeignKey(
                        name: "fk_instructor_compensation_rates_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "instructor_earnings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instructor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    penalty_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    penalty_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    earned_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instructor_earnings", x => x.id);
                    table.CheckConstraint("ck_instructor_earning_amounts", "gross_amount >= 0 AND penalty_amount >= 0 AND net_amount >= 0");
                    table.CheckConstraint("ck_instructor_earning_penalty", "penalty_percentage >= 0 AND penalty_percentage <= 100");
                    table.ForeignKey(
                        name: "fk_instructor_earnings_instructor_profiles_instructor_profile_",
                        column: x => x.instructor_profile_id,
                        principalTable: "instructor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_instructor_earnings_lesson_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "lesson_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_instructor_earnings_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "instructor_penalty_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instructor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    last_cancelled_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_penalty_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instructor_penalty_states", x => x.id);
                    table.CheckConstraint("ck_instructor_penalty_state_level", "level >= 0");
                    table.ForeignKey(
                        name: "fk_instructor_penalty_states_instructor_profiles_instructor_pr",
                        column: x => x.instructor_profile_id,
                        principalTable: "instructor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_instructor_penalty_states_lesson_sessions_last_cancelled_se",
                        column: x => x.last_cancelled_session_id,
                        principalTable: "lesson_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "instructor_penalty_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instructor_penalty_steps", x => x.id);
                    table.CheckConstraint("ck_instructor_penalty_level", "level > 0");
                    table.CheckConstraint("ck_instructor_penalty_percentage", "percentage >= 0 AND percentage <= 100");
                });

            migrationBuilder.CreateIndex(
                name: "ix_instructor_compensation_rates_organization_id_subject_id_le",
                table: "instructor_compensation_rates",
                columns: new[] { "organization_id", "subject_id", "lesson_duration_minutes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instructor_compensation_rates_subject_id",
                table: "instructor_compensation_rates",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_instructor_earnings_instructor_profile_id_earned_at",
                table: "instructor_earnings",
                columns: new[] { "instructor_profile_id", "earned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_instructor_earnings_session_id_instructor_profile_id",
                table: "instructor_earnings",
                columns: new[] { "session_id", "instructor_profile_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instructor_earnings_subject_id",
                table: "instructor_earnings",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_instructor_penalty_states_instructor_profile_id",
                table: "instructor_penalty_states",
                column: "instructor_profile_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instructor_penalty_states_last_cancelled_session_id",
                table: "instructor_penalty_states",
                column: "last_cancelled_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_instructor_penalty_steps_organization_id_level",
                table: "instructor_penalty_steps",
                columns: new[] { "organization_id", "level" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "instructor_compensation_rates");

            migrationBuilder.DropTable(
                name: "instructor_earnings");

            migrationBuilder.DropTable(
                name: "instructor_penalty_states");

            migrationBuilder.DropTable(
                name: "instructor_penalty_steps");
        }
    }
}
