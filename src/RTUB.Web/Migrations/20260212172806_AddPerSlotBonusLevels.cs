using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddPerSlotBonusLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EquippedBootsBonusLevel",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EquippedChestBonusLevel",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EquippedGlovesBonusLevel",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EquippedHeadBonusLevel",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EquippedLegsBonusLevel",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EquippedShouldersBonusLevel",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Migrate existing global EquipmentBonusLevel from StageProgresses into all 6 per-slot fields
            migrationBuilder.Sql(@"
                UPDATE Characters
                SET EquippedHeadBonusLevel = COALESCE((SELECT sp.EquipmentBonusLevel FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0),
                    EquippedShouldersBonusLevel = COALESCE((SELECT sp.EquipmentBonusLevel FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0),
                    EquippedChestBonusLevel = COALESCE((SELECT sp.EquipmentBonusLevel FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0),
                    EquippedGlovesBonusLevel = COALESCE((SELECT sp.EquipmentBonusLevel FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0),
                    EquippedLegsBonusLevel = COALESCE((SELECT sp.EquipmentBonusLevel FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0),
                    EquippedBootsBonusLevel = COALESCE((SELECT sp.EquipmentBonusLevel FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0)
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquippedBootsBonusLevel",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedChestBonusLevel",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedGlovesBonusLevel",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedHeadBonusLevel",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedLegsBonusLevel",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "EquippedShouldersBonusLevel",
                table: "Characters");
        }
    }
}
