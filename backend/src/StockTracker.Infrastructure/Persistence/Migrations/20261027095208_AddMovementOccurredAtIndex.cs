using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMovementOccurredAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_occurred_at",
                table: "stock_movements",
                column: "occurred_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_stock_movements_occurred_at",
                table: "stock_movements");
        }
    }
}
