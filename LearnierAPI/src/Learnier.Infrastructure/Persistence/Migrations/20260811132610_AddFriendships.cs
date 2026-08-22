using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnier.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "friendships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    second_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friendships", x => x.id);
                    table.CheckConstraint("ck_friendships_distinct_users", "first_user_id <> second_user_id");
                    table.CheckConstraint("ck_friendships_requester_is_participant", "requested_by_user_id = first_user_id OR requested_by_user_id = second_user_id");
                    table.CheckConstraint("ck_friendships_response_matches_status", "(status = 'Pending') = (responded_at IS NULL)");
                    table.ForeignKey(
                        name: "fk_friendships_users_first_user_id",
                        column: x => x.first_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_friendships_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_friendships_users_second_user_id",
                        column: x => x.second_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_friendships_first_user_id_second_user_id",
                table: "friendships",
                columns: new[] { "first_user_id", "second_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_friendships_requested_by_user_id_status",
                table: "friendships",
                columns: new[] { "requested_by_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_friendships_second_user_id",
                table: "friendships",
                column: "second_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "friendships");
        }
    }
}
