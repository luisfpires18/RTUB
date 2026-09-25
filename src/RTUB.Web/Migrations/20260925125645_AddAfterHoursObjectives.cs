using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddAfterHoursObjectives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AfterHoursFamilyObjectiveProgress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FamilyId = table.Column<int>(type: "INTEGER", nullable: false),
                    GameCycleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Week = table.Column<int>(type: "INTEGER", nullable: false),
                    ObjectiveKey = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Progress = table.Column<long>(type: "INTEGER", nullable: false),
                    Target = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PointsAwarded = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursFamilyObjectiveProgress", x => x.Id);
                    table.CheckConstraint("CK_AfterHoursFamilyObjectiveProgress_Points", "\"PointsAwarded\" >= 0");
                    table.CheckConstraint("CK_AfterHoursFamilyObjectiveProgress_Progress", "\"Progress\" >= 0 AND \"Progress\" <= \"Target\"");
                    table.ForeignKey(
                        name: "FK_AfterHoursFamilyObjectiveProgress_AfterHoursFamilies_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "AfterHoursFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AfterHoursFamilyObjectiveProgress_AfterHoursGameCycles_GameCycleId",
                        column: x => x.GameCycleId,
                        principalTable: "AfterHoursGameCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursPlayerObjectiveProgress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameCycleId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerCycleStateId = table.Column<int>(type: "INTEGER", nullable: false),
                    Period = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodKey = table.Column<int>(type: "INTEGER", nullable: false),
                    ObjectiveKey = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: true),
                    Progress = table.Column<long>(type: "INTEGER", nullable: false),
                    Target = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    XpAwarded = table.Column<long>(type: "INTEGER", nullable: false),
                    PointsAwarded = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursPlayerObjectiveProgress", x => x.Id);
                    table.CheckConstraint("CK_AfterHoursPlayerObjectiveProgress_Awards", "\"XpAwarded\" >= 0 AND \"PointsAwarded\" >= 0");
                    table.CheckConstraint("CK_AfterHoursPlayerObjectiveProgress_Progress", "\"Progress\" >= 0 AND \"Progress\" <= \"Target\"");
                    table.ForeignKey(
                        name: "FK_AfterHoursPlayerObjectiveProgress_AfterHoursGameCycles_GameCycleId",
                        column: x => x.GameCycleId,
                        principalTable: "AfterHoursGameCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AfterHoursPlayerObjectiveProgress_AfterHoursPlayerCycleStates_PlayerCycleStateId",
                        column: x => x.PlayerCycleStateId,
                        principalTable: "AfterHoursPlayerCycleStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursPvpObjectiveCredits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameCycleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Week = table.Column<int>(type: "INTEGER", nullable: false),
                    PvpBattleId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerStateId = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CountsForIndividual = table.Column<bool>(type: "INTEGER", nullable: false),
                    IndividualPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    FamilyId = table.Column<int>(type: "INTEGER", nullable: true),
                    CountsForFamily = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursPvpObjectiveCredits", x => x.Id);
                    table.CheckConstraint("CK_AfterHoursPvpObjectiveCredits_Points", "\"IndividualPoints\" >= 0");
                    table.ForeignKey(
                        name: "FK_AfterHoursPvpObjectiveCredits_AfterHoursGameCycles_GameCycleId",
                        column: x => x.GameCycleId,
                        principalTable: "AfterHoursGameCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AfterHoursPvpObjectiveCredits_AfterHoursPvpBattles_PvpBattleId",
                        column: x => x.PvpBattleId,
                        principalTable: "AfterHoursPvpBattles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursFamilyObjectiveProgress_GameCycleId_Week",
                table: "AfterHoursFamilyObjectiveProgress",
                columns: new[] { "GameCycleId", "Week" });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursFamilyObjectiveProgress_Instance",
                table: "AfterHoursFamilyObjectiveProgress",
                columns: new[] { "FamilyId", "GameCycleId", "Week", "ObjectiveKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPlayerObjectiveProgress_GameCycleId_Period_PeriodKey",
                table: "AfterHoursPlayerObjectiveProgress",
                columns: new[] { "GameCycleId", "Period", "PeriodKey" });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPlayerObjectiveProgress_Instance",
                table: "AfterHoursPlayerObjectiveProgress",
                columns: new[] { "PlayerCycleStateId", "Period", "PeriodKey", "ObjectiveKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPvpObjectiveCredits_FamilyTarget",
                table: "AfterHoursPvpObjectiveCredits",
                columns: new[] { "GameCycleId", "Week", "FamilyId", "DefenderUserId" },
                unique: true,
                filter: "\"CountsForFamily\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPvpObjectiveCredits_IndividualTarget",
                table: "AfterHoursPvpObjectiveCredits",
                columns: new[] { "GameCycleId", "Week", "AttackerStateId", "DefenderUserId" },
                unique: true,
                filter: "\"CountsForIndividual\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPvpObjectiveCredits_PvpBattleId",
                table: "AfterHoursPvpObjectiveCredits",
                column: "PvpBattleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AfterHoursFamilyObjectiveProgress");

            migrationBuilder.DropTable(
                name: "AfterHoursPlayerObjectiveProgress");

            migrationBuilder.DropTable(
                name: "AfterHoursPvpObjectiveCredits");
        }
    }
}
