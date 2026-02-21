using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddRareSetFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RareBootsApplied",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RareChestApplied",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RareGlovesApplied",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RareHeadApplied",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RareLegsApplied",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RareShouldersApplied",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RareBootsApplied",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "RareChestApplied",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "RareGlovesApplied",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "RareHeadApplied",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "RareLegsApplied",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "RareShouldersApplied",
                table: "Characters");
        }
    }
}
