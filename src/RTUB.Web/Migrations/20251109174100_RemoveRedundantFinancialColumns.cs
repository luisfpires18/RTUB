using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantFinancialColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalBalance",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "TotalExpenses",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "TotalIncome",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "TotalExpenses",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "TotalIncome",
                table: "Activities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FinalBalance",
                table: "Reports",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalExpenses",
                table: "Reports",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalIncome",
                table: "Reports",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                table: "Activities",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalExpenses",
                table: "Activities",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalIncome",
                table: "Activities",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
