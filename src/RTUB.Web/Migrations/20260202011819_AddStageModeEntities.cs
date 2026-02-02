using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;
/// <inheritdoc />
public partial class AddStageModeEntities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StageEnemies",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Region = table.Column<int>(type: "INTEGER", nullable: false),
                BaseHP = table.Column<int>(type: "INTEGER", nullable: false),
                BasePower = table.Column<int>(type: "INTEGER", nullable: false),
                BaseSpeed = table.Column<int>(type: "INTEGER", nullable: false),
                BaseCriticalChance = table.Column<double>(type: "REAL", nullable: false),
                SpritePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                BaseFidelisDrop = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                BeerDropChance = table.Column<double>(type: "REAL", nullable: false),
                ShotDropChance = table.Column<double>(type: "REAL", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StageEnemies", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "StageProgresses",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                CurrentStage = table.Column<int>(type: "INTEGER", nullable: false),
                HighestStage = table.Column<int>(type: "INTEGER", nullable: false),
                LastCheckpoint = table.Column<int>(type: "INTEGER", nullable: false),
                CurrentRegion = table.Column<int>(type: "INTEGER", nullable: false),
                EndlessModeUnlocked = table.Column<bool>(type: "INTEGER", nullable: false),
                TotalStagesCleared = table.Column<int>(type: "INTEGER", nullable: false),
                TotalMiniBossesDefeated = table.Column<int>(type: "INTEGER", nullable: false),
                TotalBossesDefeated = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StageProgresses", x => x.Id);
                table.ForeignKey(
                    name: "FK_StageProgresses_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StageBattles",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                StageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                StageEnemyId = table.Column<int>(type: "INTEGER", nullable: true),
                EnemyType = table.Column<int>(type: "INTEGER", nullable: false),
                Region = table.Column<int>(type: "INTEGER", nullable: false),
                EnemyName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Seed = table.Column<int>(type: "INTEGER", nullable: false),
                Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                XPReward = table.Column<int>(type: "INTEGER", nullable: false),
                FidelisReward = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                BeersDropped = table.Column<int>(type: "INTEGER", nullable: false),
                ShotsDropped = table.Column<int>(type: "INTEGER", nullable: false),
                ReplayJson = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StageBattles", x => x.Id);
                table.ForeignKey(
                    name: "FK_StageBattles_Characters_CharacterId",
                    column: x => x.CharacterId,
                    principalTable: "Characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_StageBattles_StageEnemies_StageEnemyId",
                    column: x => x.StageEnemyId,
                    principalTable: "StageEnemies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StageBattles_CharacterId",
            table: "StageBattles",
            column: "CharacterId");

        migrationBuilder.CreateIndex(
            name: "IX_StageBattles_CharacterId_CreatedAt",
            table: "StageBattles",
            columns: new[] { "CharacterId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_StageBattles_StageEnemyId",
            table: "StageBattles",
            column: "StageEnemyId");

        migrationBuilder.CreateIndex(
            name: "IX_StageBattles_StageNumber",
            table: "StageBattles",
            column: "StageNumber");

        migrationBuilder.CreateIndex(
            name: "IX_StageEnemies_Region",
            table: "StageEnemies",
            column: "Region");

        migrationBuilder.CreateIndex(
            name: "IX_StageEnemies_Type",
            table: "StageEnemies",
            column: "Type");

        migrationBuilder.CreateIndex(
            name: "IX_StageEnemies_Type_Region",
            table: "StageEnemies",
            columns: new[] { "Type", "Region" });

        migrationBuilder.CreateIndex(
            name: "IX_StageProgresses_HighestStage",
            table: "StageProgresses",
            column: "HighestStage");

        migrationBuilder.CreateIndex(
            name: "IX_StageProgresses_UserId",
            table: "StageProgresses",
            column: "UserId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "StageBattles");

        migrationBuilder.DropTable(
            name: "StageProgresses");

        migrationBuilder.DropTable(
            name: "StageEnemies");
    }
}
