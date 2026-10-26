using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    assign_reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    returned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    returned_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    return_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    return_condition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    return_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    return_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_assignments_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignments_locations_return_location_id",
                        column: x => x.return_location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignments_users_assigned_by_user_id",
                        column: x => x.assigned_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignments_users_assignee_user_id",
                        column: x => x.assignee_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignments_users_returned_by_user_id",
                        column: x => x.returned_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_assigned_by_user_id",
                table: "assignments",
                column: "assigned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_assignee_user_id_returned_at",
                table: "assignments",
                columns: new[] { "assignee_user_id", "returned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_due_date",
                table: "assignments",
                column: "due_date",
                filter: "returned_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_item_id_assigned_at",
                table: "assignments",
                columns: new[] { "item_id", "assigned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_item_id_open",
                table: "assignments",
                column: "item_id",
                unique: true,
                filter: "returned_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_return_location_id",
                table: "assignments",
                column: "return_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_returned_by_user_id",
                table: "assignments",
                column: "returned_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignments");
        }
    }
}
