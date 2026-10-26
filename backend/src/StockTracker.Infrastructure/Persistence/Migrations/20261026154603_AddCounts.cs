using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "count_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    include_children = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    started_by = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    approval_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scan_sequence = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_count_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_count_sessions_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "count_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    count_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expected_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    counted_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    line_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    resolution = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_count_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_count_lines_count_sessions_count_session_id",
                        column: x => x.count_session_id,
                        principalTable: "count_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_count_lines_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_count_lines_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_count_lines_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "count_scans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    count_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    count_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    scanned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    scanned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_count_scans", x => x.id);
                    table.ForeignKey(
                        name: "fk_count_scans_count_lines_count_line_id",
                        column: x => x.count_line_id,
                        principalTable: "count_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_count_scans_count_sessions_count_session_id",
                        column: x => x.count_session_id,
                        principalTable: "count_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_count_lines_count_session_id_location_id",
                table: "count_lines",
                columns: new[] { "count_session_id", "location_id" });

            migrationBuilder.CreateIndex(
                name: "ix_count_lines_item_id",
                table: "count_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_count_lines_location_id",
                table: "count_lines",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_count_lines_product_id",
                table: "count_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_count_scans_count_line_id",
                table: "count_scans",
                column: "count_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_count_scans_count_session_id_sequence",
                table: "count_scans",
                columns: new[] { "count_session_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_count_sessions_location_id_started_at",
                table: "count_sessions",
                columns: new[] { "location_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_count_sessions_status",
                table: "count_sessions",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "count_scans");

            migrationBuilder.DropTable(
                name: "count_lines");

            migrationBuilder.DropTable(
                name: "count_sessions");
        }
    }
}
