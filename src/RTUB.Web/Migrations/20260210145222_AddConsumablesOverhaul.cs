using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddConsumablesOverhaul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BeerDropChance",
                table: "StageEnemies",
                newName: "FinoDropChance");

            migrationBuilder.AddColumn<int>(
                name: "CanhaoDamageBoostHitsRemaining",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CigarroShieldHitsRemaining",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanhaoDamageBoostHitsRemaining",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "CigarroShieldHitsRemaining",
                table: "Characters");

            migrationBuilder.RenameColumn(
                name: "FinoDropChance",
                table: "StageEnemies",
                newName: "BeerDropChance");
        }
    }
}
