using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddConsumableUpgrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CanhaoTimerUpgrades",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CigarroDodgeUpgrades",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PenaltyTimerUpgrades",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ShotStatBuffUpgrades",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanhaoTimerUpgrades",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "CigarroDodgeUpgrades",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PenaltyTimerUpgrades",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ShotStatBuffUpgrades",
                table: "Characters");
        }
    }
}
