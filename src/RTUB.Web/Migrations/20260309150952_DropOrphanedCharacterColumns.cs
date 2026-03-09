using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <summary>
    /// Drops orphaned columns from the Characters table that were removed from the 
    /// Character entity but never dropped from the database, causing NOT NULL constraint 
    /// failures when inserting new characters.
    /// </summary>
    public partial class DropOrphanedCharacterColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeavyAttackUpgrades",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SpecialAttackUpgrades",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "FidelisEarnedUpgrades",
                table: "Characters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HeavyAttackUpgrades",
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

            migrationBuilder.AddColumn<int>(
                name: "FidelisEarnedUpgrades",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
