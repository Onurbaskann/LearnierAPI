using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnier.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogSchedulingAndBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instructor_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bio = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    default_hourly_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    default_hourly_rate_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instructor_profiles", x => x.id);
                    table.CheckConstraint("ck_instructor_profiles_rate_currency_paired", "(default_hourly_rate IS NULL) = (default_hourly_rate_currency IS NULL)");
                    table.CheckConstraint("ck_instructor_profiles_rate_not_negative", "default_hourly_rate IS NULL OR default_hourly_rate >= 0");
                    table.ForeignKey(
                        name: "fk_instructor_profiles_memberships_membership_id",
                        column: x => x.membership_id,
                        principalTable: "organization_memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_customer_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_customers", x => x.id);
                    table.CheckConstraint("ck_payment_customers_single_owner", "(user_id IS NULL) <> (organization_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_payment_customers_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_customers_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subjects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subjects", x => x.id);
                    table.CheckConstraint("ck_subjects_parent_not_self", "parent_subject_id IS NULL OR parent_subject_id <> id");
                    table.ForeignKey(
                        name: "fk_subjects_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_subjects_subjects_parent_subject_id",
                        column: x => x.parent_subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    catalog_access = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_plans", x => x.id);
                    table.ForeignKey(
                        name: "fk_subscription_plans_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "instructor_availabilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instructor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    start_local_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_local_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instructor_availabilities", x => x.id);
                    table.CheckConstraint("ck_instructor_availabilities_time_range", "end_local_time > start_local_time");
                    table.CheckConstraint("ck_instructor_availabilities_valid_range", "valid_until IS NULL OR valid_until >= valid_from");
                    table.ForeignKey(
                        name: "fk_instructor_availabilities_instructor_profiles_instructor_pr",
                        column: x => x.instructor_profile_id,
                        principalTable: "instructor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "instructor_availability_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instructor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    override_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_local_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    end_local_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    override_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instructor_availability_overrides", x => x.id);
                    table.CheckConstraint("ck_availability_overrides_time_range", "start_local_time IS NULL OR end_local_time > start_local_time");
                    table.CheckConstraint("ck_availability_overrides_times_paired", "(start_local_time IS NULL) = (end_local_time IS NULL)");
                    table.ForeignKey(
                        name: "fk_instructor_availability_overrides_instructor_profiles_instr",
                        column: x => x.instructor_profile_id,
                        principalTable: "instructor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "levels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_levels", x => x.id);
                    table.ForeignKey(
                        name: "fk_levels_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_entitlements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entitlement_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    session_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: true),
                    reset_period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_entitlements", x => x.id);
                    table.CheckConstraint("ck_plan_entitlements_credit_requires_quantity", "entitlement_type <> 'LessonCredit' OR quantity IS NOT NULL");
                    table.CheckConstraint("ck_plan_entitlements_quantity_positive", "quantity IS NULL OR quantity > 0");
                    table.ForeignKey(
                        name: "fk_plan_entitlements_subscription_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    billing_interval = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    billing_interval_count = table.Column<int>(type: "integer", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_prices", x => x.id);
                    table.CheckConstraint("ck_plan_prices_amount_not_negative", "amount >= 0");
                    table.CheckConstraint("ck_plan_prices_interval_count_positive", "billing_interval_count > 0");
                    table.CheckConstraint("ck_plan_prices_valid_range", "valid_until IS NULL OR valid_until >= valid_from");
                    table.ForeignKey(
                        name: "fk_plan_prices_subscription_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_subject_access",
                columns: table => new
                {
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_subject_access", x => new { x.plan_id, x.subject_id });
                    table.ForeignKey(
                        name: "fk_plan_subject_access_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_plan_subject_access_subscription_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "courses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    course_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    default_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    min_participants = table.Column<int>(type: "integer", nullable: false),
                    max_participants = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_courses", x => x.id);
                    table.CheckConstraint("ck_courses_duration_positive", "default_duration_minutes > 0");
                    table.CheckConstraint("ck_courses_participant_range", "min_participants >= 1 AND max_participants >= min_participants");
                    table.ForeignKey(
                        name: "fk_courses_levels_level_id",
                        column: x => x.level_id,
                        principalTable: "levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_courses_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_courses_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "instructor_subjects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instructor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instructor_subjects", x => x.id);
                    table.ForeignKey(
                        name: "fk_instructor_subjects_instructor_profiles_instructor_profile_",
                        column: x => x.instructor_profile_id,
                        principalTable: "instructor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_instructor_subjects_levels_level_id",
                        column: x => x.level_id,
                        principalTable: "levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_instructor_subjects_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    plan_price_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    current_period_start = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    current_period_end = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    cancel_at_period_end = table.Column<bool>(type: "boolean", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    payment_provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    provider_subscription_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.id);
                    table.CheckConstraint("ck_subscriptions_period_range", "current_period_end > current_period_start");
                    table.CheckConstraint("ck_subscriptions_single_subscriber", "(subscriber_user_id IS NULL) <> (subscriber_organization_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_subscriptions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_subscriptions_organizations_subscriber_organization_id",
                        column: x => x.subscriber_organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscriptions_plan_prices_plan_price_id",
                        column: x => x.plan_price_id,
                        principalTable: "plan_prices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscriptions_users_subscriber_user_id",
                        column: x => x.subscriber_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    delivery_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: true),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_groups", x => x.id);
                    table.CheckConstraint("ck_class_groups_capacity_positive", "capacity > 0");
                    table.CheckConstraint("ck_class_groups_date_range", "starts_on IS NULL OR ends_on IS NULL OR ends_on >= starts_on");
                    table.ForeignKey(
                        name: "fk_class_groups_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_class_groups_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "course_modules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_modules", x => x.id);
                    table.ForeignKey(
                        name: "fk_course_modules_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "learner_course_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    learner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completion_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learner_course_progress", x => x.id);
                    table.CheckConstraint("ck_learner_course_progress_percentage_range", "completion_percentage >= 0 AND completion_percentage <= 100");
                    table.ForeignKey(
                        name: "fk_learner_course_progress_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_learner_course_progress_levels_current_level_id",
                        column: x => x.current_level_id,
                        principalTable: "levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_learner_course_progress_users_learner_user_id",
                        column: x => x.learner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_course_access",
                columns: table => new
                {
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_course_access", x => new { x.plan_id, x.course_id });
                    table.ForeignKey(
                        name: "fk_plan_course_access_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_plan_course_access_subscription_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    payment_provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_payment_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.CheckConstraint("ck_payments_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "fk_payments_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_users_payer_user_id",
                        column: x => x.payer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_seats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_seats", x => x.id);
                    table.CheckConstraint("ck_subscription_seats_revoked_after_assigned", "revoked_at IS NULL OR revoked_at >= assigned_at");
                    table.ForeignKey(
                        name: "fk_subscription_seats_memberships_membership_id",
                        column: x => x.membership_id,
                        principalTable: "organization_memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_subscription_seats_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "class_group_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    enrolled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_group_members", x => x.id);
                    table.CheckConstraint("ck_class_group_members_left_after_enrolled", "left_at IS NULL OR left_at >= enrolled_at");
                    table.ForeignKey(
                        name: "fk_class_group_members_class_groups_class_group_id",
                        column: x => x.class_group_id,
                        principalTable: "class_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_class_group_members_users_learner_user_id",
                        column: x => x.learner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "course_lessons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    estimated_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_lessons", x => x.id);
                    table.CheckConstraint("ck_course_lessons_duration_positive", "estimated_duration_minutes > 0");
                    table.ForeignKey(
                        name: "fk_course_lessons_course_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "course_modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refunds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_refund_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refunds", x => x.id);
                    table.CheckConstraint("ck_refunds_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "fk_refunds_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lesson_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    course_lesson_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    minimum_participants = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    meeting_provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    meeting_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    booking_opens_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    booking_closes_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancellation_deadline_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_sessions", x => x.id);
                    table.CheckConstraint("ck_lesson_sessions_booking_window", "booking_opens_at IS NULL\n                  OR booking_closes_at IS NULL\n                  OR booking_closes_at >= booking_opens_at");
                    table.CheckConstraint("ck_lesson_sessions_capacity_positive", "capacity > 0");
                    table.CheckConstraint("ck_lesson_sessions_minimum_participants", "minimum_participants >= 0 AND minimum_participants <= capacity");
                    table.CheckConstraint("ck_lesson_sessions_time_range", "ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_lesson_sessions_class_groups_class_group_id",
                        column: x => x.class_group_id,
                        principalTable: "class_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lesson_sessions_course_lessons_course_lesson_id",
                        column: x => x.course_lesson_id,
                        principalTable: "course_lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lesson_sessions_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lesson_sessions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lesson_completions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    learner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    completion_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_completions", x => x.id);
                    table.ForeignKey(
                        name: "fk_lesson_completions_course_lessons_course_lesson_id",
                        column: x => x.course_lesson_id,
                        principalTable: "course_lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lesson_completions_lesson_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "lesson_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lesson_completions_users_learner_user_id",
                        column: x => x.learner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_feedback",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_instructor_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_feedback", x => x.id);
                    table.CheckConstraint("ck_session_feedback_rating_range", "rating >= 1 AND rating <= 5");
                    table.ForeignKey(
                        name: "fk_session_feedback_instructor_profiles_target_instructor_prof",
                        column: x => x.target_instructor_profile_id,
                        principalTable: "instructor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_session_feedback_lesson_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "lesson_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_session_feedback_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_instructors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instructor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_instructors", x => x.id);
                    table.ForeignKey(
                        name: "fk_session_instructors_instructor_profiles_instructor_profile_",
                        column: x => x.instructor_profile_id,
                        principalTable: "instructor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_session_instructors_lesson_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "lesson_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credit_ledger", x => x.id);
                    table.CheckConstraint("ck_credit_ledger_quantity_not_zero", "quantity <> 0");
                    table.ForeignKey(
                        name: "fk_credit_ledger_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_credit_ledger_users_learner_user_id",
                        column: x => x.learner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booked_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    access_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credit_ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    booked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_bookings", x => x.id);
                    table.CheckConstraint("ck_session_bookings_cancelled_at_present", "(status = 'Cancelled') = (cancelled_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_session_bookings_credit_ledger_credit_ledger_entry_id",
                        column: x => x.credit_ledger_entry_id,
                        principalTable: "credit_ledger",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_session_bookings_lesson_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "lesson_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_session_bookings_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_session_bookings_users_booked_by_user_id",
                        column: x => x.booked_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_session_bookings_users_learner_user_id",
                        column: x => x.learner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_attendances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    left_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    attended_minutes = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    marked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_attendances", x => x.id);
                    table.CheckConstraint("ck_session_attendances_minutes_not_negative", "attended_minutes >= 0");
                    table.CheckConstraint("ck_session_attendances_time_range", "joined_at IS NULL OR left_at IS NULL OR left_at >= joined_at");
                    table.ForeignKey(
                        name: "fk_session_attendances_session_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "session_bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_session_attendances_users_marked_by_user_id",
                        column: x => x.marked_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_class_group_members_class_group_id_learner_user_id",
                table: "class_group_members",
                columns: new[] { "class_group_id", "learner_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_class_group_members_learner_user_id_status",
                table: "class_group_members",
                columns: new[] { "learner_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_class_groups_course_id",
                table: "class_groups",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_groups_organization_id_status",
                table: "class_groups",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_course_lessons_module_id_sort_order",
                table: "course_lessons",
                columns: new[] { "module_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_course_modules_course_id_sort_order",
                table: "course_modules",
                columns: new[] { "course_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_courses_level_id",
                table: "courses",
                column: "level_id");

            migrationBuilder.CreateIndex(
                name: "ix_courses_organization_id_subject_id_status",
                table: "courses",
                columns: new[] { "organization_id", "subject_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_courses_subject_id",
                table: "courses",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_credit_ledger_booking_id",
                table: "credit_ledger",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_credit_ledger_learner_user_id_session_type",
                table: "credit_ledger",
                columns: new[] { "learner_user_id", "session_type" });

            migrationBuilder.CreateIndex(
                name: "ix_credit_ledger_subscription_id_learner_user_id_expires_at",
                table: "credit_ledger",
                columns: new[] { "subscription_id", "learner_user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_instructor_availabilities_instructor_profile_id_day_of_week",
                table: "instructor_availabilities",
                columns: new[] { "instructor_profile_id", "day_of_week", "valid_from" });

            migrationBuilder.CreateIndex(
                name: "ix_instructor_availability_overrides_instructor_profile_id_ove",
                table: "instructor_availability_overrides",
                columns: new[] { "instructor_profile_id", "override_date" });

            migrationBuilder.CreateIndex(
                name: "ix_instructor_profiles_membership_id",
                table: "instructor_profiles",
                column: "membership_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instructor_subjects_instructor_profile_id_subject_id_level_",
                table: "instructor_subjects",
                columns: new[] { "instructor_profile_id", "subject_id", "level_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_instructor_subjects_level_id",
                table: "instructor_subjects",
                column: "level_id");

            migrationBuilder.CreateIndex(
                name: "ix_instructor_subjects_subject_id_status",
                table: "instructor_subjects",
                columns: new[] { "subject_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_learner_course_progress_course_id",
                table: "learner_course_progress",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_learner_course_progress_current_level_id",
                table: "learner_course_progress",
                column: "current_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_learner_course_progress_learner_user_id_course_id",
                table: "learner_course_progress",
                columns: new[] { "learner_user_id", "course_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lesson_completions_course_lesson_id",
                table: "lesson_completions",
                column: "course_lesson_id");

            migrationBuilder.CreateIndex(
                name: "ix_lesson_completions_learner_user_id_course_lesson_id",
                table: "lesson_completions",
                columns: new[] { "learner_user_id", "course_lesson_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lesson_completions_session_id",
                table: "lesson_completions",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_lesson_sessions_class_group_id_starts_at",
                table: "lesson_sessions",
                columns: new[] { "class_group_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lesson_sessions_course_id",
                table: "lesson_sessions",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_lesson_sessions_course_lesson_id",
                table: "lesson_sessions",
                column: "course_lesson_id");

            migrationBuilder.CreateIndex(
                name: "ix_lesson_sessions_organization_id_starts_at_status",
                table: "lesson_sessions",
                columns: new[] { "organization_id", "starts_at", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_levels_subject_id_code",
                table: "levels",
                columns: new[] { "subject_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_levels_subject_id_sort_order",
                table: "levels",
                columns: new[] { "subject_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_customers_organization_id",
                table: "payment_customers",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_customers_provider_provider_customer_id",
                table: "payment_customers",
                columns: new[] { "provider", "provider_customer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_customers_user_id",
                table: "payment_customers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_payer_user_id",
                table: "payments",
                column: "payer_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_payment_provider_provider_payment_id",
                table: "payments",
                columns: new[] { "payment_provider", "provider_payment_id" },
                unique: true,
                filter: "provider_payment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payments_subscription_id_status",
                table: "payments",
                columns: new[] { "subscription_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_plan_course_access_course_id",
                table: "plan_course_access",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_entitlements_plan_id_entitlement_type_session_type",
                table: "plan_entitlements",
                columns: new[] { "plan_id", "entitlement_type", "session_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plan_prices_plan_id_currency_status",
                table: "plan_prices",
                columns: new[] { "plan_id", "currency", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_plan_subject_access_subject_id",
                table: "plan_subject_access",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_refunds_payment_id",
                table: "refunds",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_attendances_booking_id",
                table: "session_attendances",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_attendances_marked_by_user_id",
                table: "session_attendances",
                column: "marked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_bookings_booked_by_user_id",
                table: "session_bookings",
                column: "booked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_bookings_credit_ledger_entry_id",
                table: "session_bookings",
                column: "credit_ledger_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_bookings_learner_user_id_status",
                table: "session_bookings",
                columns: new[] { "learner_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_session_bookings_session_id_learner_user_id",
                table: "session_bookings",
                columns: new[] { "session_id", "learner_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_bookings_session_id_status",
                table: "session_bookings",
                columns: new[] { "session_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_session_bookings_subscription_id",
                table: "session_bookings",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_feedback_author_user_id",
                table: "session_feedback",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_feedback_session_id_author_user_id_target_instructo",
                table: "session_feedback",
                columns: new[] { "session_id", "author_user_id", "target_instructor_profile_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_session_feedback_target_instructor_profile_id",
                table: "session_feedback",
                column: "target_instructor_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_instructors_instructor_profile_id_session_id",
                table: "session_instructors",
                columns: new[] { "instructor_profile_id", "session_id" });

            migrationBuilder.CreateIndex(
                name: "ix_session_instructors_session_id_instructor_profile_id",
                table: "session_instructors",
                columns: new[] { "session_id", "instructor_profile_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subjects_organization_id_slug",
                table: "subjects",
                columns: new[] { "organization_id", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subjects_parent_subject_id",
                table: "subjects",
                column: "parent_subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_plans_organization_id_status",
                table: "subscription_plans",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_subscription_seats_membership_id",
                table: "subscription_seats",
                column: "membership_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_seats_subscription_id_membership_id",
                table: "subscription_seats",
                columns: new[] { "subscription_id", "membership_id" },
                unique: true,
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_organization_id",
                table: "subscriptions",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_payment_provider_provider_subscription_id",
                table: "subscriptions",
                columns: new[] { "payment_provider", "provider_subscription_id" },
                unique: true,
                filter: "provider_subscription_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_plan_price_id",
                table: "subscriptions",
                column: "plan_price_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_subscriber_organization_id_status",
                table: "subscriptions",
                columns: new[] { "subscriber_organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_subscriber_user_id_status",
                table: "subscriptions",
                columns: new[] { "subscriber_user_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "fk_credit_ledger_session_bookings_booking_id",
                table: "credit_ledger",
                column: "booking_id",
                principalTable: "session_bookings",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_lesson_sessions_class_groups_class_group_id",
                table: "lesson_sessions");

            migrationBuilder.DropForeignKey(
                name: "fk_course_modules_courses_course_id",
                table: "course_modules");

            migrationBuilder.DropForeignKey(
                name: "fk_lesson_sessions_courses_course_id",
                table: "lesson_sessions");

            migrationBuilder.DropForeignKey(
                name: "fk_course_lessons_course_modules_module_id",
                table: "course_lessons");

            migrationBuilder.DropForeignKey(
                name: "fk_credit_ledger_session_bookings_booking_id",
                table: "credit_ledger");

            migrationBuilder.DropTable(
                name: "class_group_members");

            migrationBuilder.DropTable(
                name: "instructor_availabilities");

            migrationBuilder.DropTable(
                name: "instructor_availability_overrides");

            migrationBuilder.DropTable(
                name: "instructor_subjects");

            migrationBuilder.DropTable(
                name: "learner_course_progress");

            migrationBuilder.DropTable(
                name: "lesson_completions");

            migrationBuilder.DropTable(
                name: "payment_customers");

            migrationBuilder.DropTable(
                name: "plan_course_access");

            migrationBuilder.DropTable(
                name: "plan_entitlements");

            migrationBuilder.DropTable(
                name: "plan_subject_access");

            migrationBuilder.DropTable(
                name: "refunds");

            migrationBuilder.DropTable(
                name: "session_attendances");

            migrationBuilder.DropTable(
                name: "session_feedback");

            migrationBuilder.DropTable(
                name: "session_instructors");

            migrationBuilder.DropTable(
                name: "subscription_seats");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "instructor_profiles");

            migrationBuilder.DropTable(
                name: "class_groups");

            migrationBuilder.DropTable(
                name: "courses");

            migrationBuilder.DropTable(
                name: "levels");

            migrationBuilder.DropTable(
                name: "subjects");

            migrationBuilder.DropTable(
                name: "course_modules");

            migrationBuilder.DropTable(
                name: "session_bookings");

            migrationBuilder.DropTable(
                name: "credit_ledger");

            migrationBuilder.DropTable(
                name: "lesson_sessions");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "course_lessons");

            migrationBuilder.DropTable(
                name: "plan_prices");

            migrationBuilder.DropTable(
                name: "subscription_plans");
        }
    }
}
