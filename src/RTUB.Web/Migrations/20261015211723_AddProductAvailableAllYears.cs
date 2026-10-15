using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddProductAvailableAllYears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #221: every existing product stays year-specific (the fiscal year of its CreatedAt), and so does any
            // product the previous release (N-1, which does not know this column) creates after an app rollback.
            // Nothing else changes: prices, stock and reservations are untouched.
            migrationBuilder.AddColumn<bool>(
                name: "AvailableAllYears",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailableAllYears",
                table: "Products");
        }
    }
}
