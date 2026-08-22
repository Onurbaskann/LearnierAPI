using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnier.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLearnerOnboardingProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learner_onboarding_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estimated_level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    learning_goal = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    self_assessment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    lesson_focus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    instructor_preference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    difficulty_areas = table.Column<string[]>(type: "text[]", nullable: false),
                    availability_preferences = table.Column<string[]>(type: "text[]", nullable: false),
                    weekly_lesson_goal = table.Column<int>(type: "integer", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learner_onboarding_profiles", x => x.id);
                    table.CheckConstraint("ck_learner_onboarding_profiles_weekly_goal", "weekly_lesson_goal >= 1 AND weekly_lesson_goal <= 7");
                    table.ForeignKey(
                        name: "fk_learner_onboarding_profiles_levels_estimated_level_id",
                        column: x => x.estimated_level_id,
                        principalTable: "levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_learner_onboarding_profiles_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_learner_onboarding_profiles_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learner_onboarding_profiles_users_learner_user_id",
                        column: x => x.learner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_learner_onboarding_profiles_estimated_level_id",
                table: "learner_onboarding_profiles",
                column: "estimated_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_learner_onboarding_profiles_learner_user_id",
                table: "learner_onboarding_profiles",
                column: "learner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_learner_onboarding_profiles_organization_id_learner_user_id",
                table: "learner_onboarding_profiles",
                columns: new[] { "organization_id", "learner_user_id", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_learner_onboarding_profiles_subject_id",
                table: "learner_onboarding_profiles",
                column: "subject_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "learner_onboarding_profiles");
        }
    }
}
