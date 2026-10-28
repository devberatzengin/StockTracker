using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMovementReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "reverses_movement_id",
                table: "stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_reverses_movement_id",
                table: "stock_movements",
                column: "reverses_movement_id",
                unique: true,
                filter: "reverses_movement_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_stock_movements_stock_movements_reverses_movement_id",
                table: "stock_movements",
                column: "reverses_movement_id",
                principalTable: "stock_movements",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_stock_movements_stock_movements_reverses_movement_id",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "ix_stock_movements_reverses_movement_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "reverses_movement_id",
                table: "stock_movements");
        }
    }
}
