using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddPenaltyBuffActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PenaltyBuffActive",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Scale CriticalUpgrades for 0.005 multiplier (100 levels for 50% max)
            // Double existing values since multiplier changed from 0.01 to 0.005, cap at 100
            migrationBuilder.Sql("UPDATE Characters SET CriticalUpgrades = MIN(CriticalUpgrades * 2, 100);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PenaltyBuffActive",
                table: "Characters");
        }
    }
}
