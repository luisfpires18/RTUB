using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddAfterHoursTrainingAndGear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EquippedOutfitKey",
                table: "AfterHoursPlayerCycleStates",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquippedVehicleToolKey",
                table: "AfterHoursPlayerCycleStates",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquippedWeaponKey",
                table: "AfterHoursPlayerCycleStates",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrainingPoints",
                table: "AfterHoursPlayerCycleStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TrainingPointsDay",
                table: "AfterHoursPlayerCycleStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GearKey",
                table: "AfterHoursPlayerActionReceipts",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Skill",
                table: "AfterHoursPlayerActionReceipts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SkillRankAfter",
                table: "AfterHoursPlayerActionReceipts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AfterHoursPlayerGear",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerCycleStateId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemKey = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursPlayerGear", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfterHoursPlayerGear_AfterHoursPlayerCycleStates_PlayerCycleStateId",
                        column: x => x.PlayerCycleStateId,
                        principalTable: "AfterHoursPlayerCycleStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPlayerGear_State_Item",
                table: "AfterHoursPlayerGear",
                columns: new[] { "PlayerCycleStateId", "ItemKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AfterHoursPlayerGear");

            migrationBuilder.DropColumn(
                name: "EquippedOutfitKey",
                table: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropColumn(
                name: "EquippedVehicleToolKey",
                table: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropColumn(
                name: "EquippedWeaponKey",
                table: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropColumn(
                name: "TrainingPoints",
                table: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropColumn(
                name: "TrainingPointsDay",
                table: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropColumn(
                name: "GearKey",
                table: "AfterHoursPlayerActionReceipts");

            migrationBuilder.DropColumn(
                name: "Skill",
                table: "AfterHoursPlayerActionReceipts");

            migrationBuilder.DropColumn(
                name: "SkillRankAfter",
                table: "AfterHoursPlayerActionReceipts");
        }
    }
}
