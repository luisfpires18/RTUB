using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddImprovementsAndPowers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EnergyAmountUpgrades",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EnergyRegenUpgrades",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FidelisEarnedUpgrades",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HeavyAttackUpgrades",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ShotBuffUpgrades",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SpecialAttackUpgrades",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnergyAmountUpgrades",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EnergyRegenUpgrades",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "FidelisEarnedUpgrades",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "HeavyAttackUpgrades",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ShotBuffUpgrades",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SpecialAttackUpgrades",
                table: "Characters");
        }
    }
}
