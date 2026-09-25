using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddAfterHoursYearbook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AfterHoursCycleArchives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameCycleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    FiscalYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    FiscalYearLabel = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    StartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Official = table.Column<bool>(type: "INTEGER", nullable: false),
                    NextGameCycleId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursCycleArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfterHoursCycleArchives_AfterHoursGameCycles_GameCycleId",
                        column: x => x.GameCycleId,
                        principalTable: "AfterHoursGameCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AfterHoursCycleArchives_AfterHoursGameCycles_NextGameCycleId",
                        column: x => x.NextGameCycleId,
                        principalTable: "AfterHoursGameCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AfterHoursCycleArchives_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursYearbookFamilies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CycleArchiveId = table.Column<int>(type: "INTEGER", nullable: false),
                    FamilyId = table.Column<int>(type: "INTEGER", nullable: false),
                    FamilyName = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    AnnualScore = table.Column<int>(type: "INTEGER", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    ScoringWeeks = table.Column<int>(type: "INTEGER", nullable: false),
                    IsChampion = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursYearbookFamilies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfterHoursYearbookFamilies_AfterHoursCycleArchives_CycleArchiveId",
                        column: x => x.CycleArchiveId,
                        principalTable: "AfterHoursCycleArchives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AfterHoursYearbookFamilies_AfterHoursFamilies_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "AfterHoursFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursYearbookPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CycleArchiveId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    XP = table.Column<long>(type: "INTEGER", nullable: false),
                    Toughness = table.Column<int>(type: "INTEGER", nullable: false),
                    Stealth = table.Column<int>(type: "INTEGER", nullable: false),
                    Smarts = table.Column<int>(type: "INTEGER", nullable: false),
                    Charisma = table.Column<int>(type: "INTEGER", nullable: false),
                    AnnualScore = table.Column<int>(type: "INTEGER", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    ScoringWeeks = table.Column<int>(type: "INTEGER", nullable: false),
                    FamilyId = table.Column<int>(type: "INTEGER", nullable: true),
                    FamilyName = table.Column<string>(type: "TEXT", maxLength: 24, nullable: true),
                    IsChampion = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursYearbookPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfterHoursYearbookPlayers_AfterHoursCycleArchives_CycleArchiveId",
                        column: x => x.CycleArchiveId,
                        principalTable: "AfterHoursCycleArchives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AfterHoursYearbookPlayers_AfterHoursFamilies_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "AfterHoursFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursYearbookFamilyMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YearbookFamilyEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursYearbookFamilyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfterHoursYearbookFamilyMembers_AfterHoursYearbookFamilies_YearbookFamilyEntryId",
                        column: x => x.YearbookFamilyEntryId,
                        principalTable: "AfterHoursYearbookFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursCycleArchives_Cycle",
                table: "AfterHoursCycleArchives",
                column: "GameCycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursCycleArchives_FiscalYearId",
                table: "AfterHoursCycleArchives",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursCycleArchives_NextCycle",
                table: "AfterHoursCycleArchives",
                column: "NextGameCycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursYearbookFamilies_Archive_Family",
                table: "AfterHoursYearbookFamilies",
                columns: new[] { "CycleArchiveId", "FamilyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursYearbookFamilies_FamilyId",
                table: "AfterHoursYearbookFamilies",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursYearbookFamilyMembers_Entry_User",
                table: "AfterHoursYearbookFamilyMembers",
                columns: new[] { "YearbookFamilyEntryId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursYearbookPlayers_Archive_User",
                table: "AfterHoursYearbookPlayers",
                columns: new[] { "CycleArchiveId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursYearbookPlayers_FamilyId",
                table: "AfterHoursYearbookPlayers",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursYearbookPlayers_UserId",
                table: "AfterHoursYearbookPlayers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AfterHoursYearbookFamilyMembers");

            migrationBuilder.DropTable(
                name: "AfterHoursYearbookPlayers");

            migrationBuilder.DropTable(
                name: "AfterHoursYearbookFamilies");

            migrationBuilder.DropTable(
                name: "AfterHoursCycleArchives");
        }
    }
}
