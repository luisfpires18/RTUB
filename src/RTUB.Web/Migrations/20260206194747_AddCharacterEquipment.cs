using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddCharacterEquipment : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "EquipmentCriticalBonus",
                table: "Characters",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "EquipmentDefenseBonus",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EquipmentHPBonus",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EquipmentPowerBonus",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EquipmentSpeedBonus",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EquippedBoots",
                table: "Characters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EquippedChest",
                table: "Characters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EquippedGloves",
                table: "Characters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EquippedHead",
                table: "Characters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EquippedInstrument",
                table: "Characters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EquippedLegs",
                table: "Characters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EquippedShoulders",
                table: "Characters",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquipmentCriticalBonus",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquipmentDefenseBonus",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquipmentHPBonus",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquipmentPowerBonus",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquipmentSpeedBonus",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedBoots",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedChest",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedGloves",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedHead",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedInstrument",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedLegs",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedShoulders",
                table: "Characters");
        }
}
