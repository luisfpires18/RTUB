using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Data.Migrations;

/// <inheritdoc />
public partial class AddStageModeEntities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "EquippedInstrument",
            table: "Characters",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Stages",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                StageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                RequiredLevel = table.Column<int>(type: "INTEGER", nullable: false),
                EnemyConfigKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                RewardInstrument = table.Column<int>(type: "INTEGER", nullable: false),
                FidelisReward = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                BeerDropChance = table.Column<double>(type: "REAL", nullable: false),
                ShotDropChance = table.Column<double>(type: "REAL", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Stages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CharacterStageProgress",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                StageId = table.Column<int>(type: "INTEGER", nullable: false),
                CompletionCount = table.Column<int>(type: "INTEGER", nullable: false),
                FirstCompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastCompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                InstrumentClaimed = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CharacterStageProgress", x => x.Id);
                table.ForeignKey(
                    name: "FK_CharacterStageProgress_Characters_CharacterId",
                    column: x => x.CharacterId,
                    principalTable: "Characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CharacterStageProgress_Stages_StageId",
                    column: x => x.StageId,
                    principalTable: "Stages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CharacterStageProgress_CharacterId",
            table: "CharacterStageProgress",
            column: "CharacterId");

        migrationBuilder.CreateIndex(
            name: "IX_CharacterStageProgress_CharacterId_StageId",
            table: "CharacterStageProgress",
            columns: new[] { "CharacterId", "StageId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CharacterStageProgress_StageId",
            table: "CharacterStageProgress",
            column: "StageId");

        migrationBuilder.CreateIndex(
            name: "IX_Stages_StageNumber",
            table: "Stages",
            column: "StageNumber",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CharacterStageProgress");

        migrationBuilder.DropTable(
            name: "Stages");

        migrationBuilder.DropColumn(
            name: "EquippedInstrument",
            table: "Characters");
    }
}
