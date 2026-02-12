using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentSlotQualities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "EquippedBootsQuality",
                table: "Characters",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "EquippedChestQuality",
                table: "Characters",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "EquippedGlovesQuality",
                table: "Characters",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "EquippedHeadQuality",
                table: "Characters",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "EquippedLegsQuality",
                table: "Characters",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "EquippedShouldersQuality",
                table: "Characters",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquippedBootsQuality",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedChestQuality",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedGlovesQuality",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedHeadQuality",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedLegsQuality",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedShouldersQuality",
                table: "Characters");
        }
    }
}
