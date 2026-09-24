using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddAfterHoursCycleAndPlayerState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AfterHoursGameCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FiscalYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursGameCycles", x => x.Id);
                    table.CheckConstraint("CK_AfterHoursGameCycles_EndAfterStart", "\"EndUtc\" > \"StartUtc\"");
                    table.ForeignKey(
                        name: "FK_AfterHoursGameCycles_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursPlayerCycleStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameCycleId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    XP = table.Column<long>(type: "INTEGER", nullable: false),
                    WalletCash = table.Column<long>(type: "INTEGER", nullable: false),
                    BankCash = table.Column<long>(type: "INTEGER", nullable: false),
                    Energy = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxEnergy = table.Column<int>(type: "INTEGER", nullable: false),
                    EnergyUpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Heat = table.Column<int>(type: "INTEGER", nullable: false),
                    HeatUpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Toughness = table.Column<int>(type: "INTEGER", nullable: false),
                    Stealth = table.Column<int>(type: "INTEGER", nullable: false),
                    Smarts = table.Column<int>(type: "INTEGER", nullable: false),
                    Charisma = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursPlayerCycleStates", x => x.Id);
                    table.CheckConstraint("CK_AfterHoursPlayerCycleStates_BankCash", "\"BankCash\" >= 0");
                    table.CheckConstraint("CK_AfterHoursPlayerCycleStates_Energy", "\"Energy\" >= 0");
                    table.CheckConstraint("CK_AfterHoursPlayerCycleStates_Heat", "\"Heat\" >= 0");
                    table.CheckConstraint("CK_AfterHoursPlayerCycleStates_WalletCash", "\"WalletCash\" >= 0");
                    table.ForeignKey(
                        name: "FK_AfterHoursPlayerCycleStates_AfterHoursGameCycles_GameCycleId",
                        column: x => x.GameCycleId,
                        principalTable: "AfterHoursGameCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AfterHoursPlayerCycleStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursGameCycles_FiscalYearId",
                table: "AfterHoursGameCycles",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursGameCycles_SingleActive",
                table: "AfterHoursGameCycles",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPlayerCycleStates_Cycle_User",
                table: "AfterHoursPlayerCycleStates",
                columns: new[] { "GameCycleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPlayerCycleStates_UserId",
                table: "AfterHoursPlayerCycleStates",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropTable(
                name: "AfterHoursGameCycles");
        }
    }
}
