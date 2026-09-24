using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddAfterHoursSoloLoop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "CoverJobDailyUsedOn",
                table: "AfterHoursPlayerCycleStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "JailUntilUtc",
                table: "AfterHoursPlayerCycleStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AfterHoursPlayerActionReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerCycleStateId = table.Column<int>(type: "INTEGER", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    Request = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CrimeId = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    Approach = table.Column<int>(type: "INTEGER", nullable: true),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    SuccessChance = table.Column<int>(type: "INTEGER", nullable: true),
                    SuccessRoll = table.Column<int>(type: "INTEGER", nullable: true),
                    Jailed = table.Column<bool>(type: "INTEGER", nullable: false),
                    JailUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WalletDelta = table.Column<long>(type: "INTEGER", nullable: false),
                    BankDelta = table.Column<long>(type: "INTEGER", nullable: false),
                    XpDelta = table.Column<long>(type: "INTEGER", nullable: false),
                    HeatDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    EnergyDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    LevelBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    LevelAfter = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursPlayerActionReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfterHoursPlayerActionReceipts_AfterHoursPlayerCycleStates_PlayerCycleStateId",
                        column: x => x.PlayerCycleStateId,
                        principalTable: "AfterHoursPlayerCycleStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPlayerActionReceipts_State_Key",
                table: "AfterHoursPlayerActionReceipts",
                columns: new[] { "PlayerCycleStateId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AfterHoursPlayerActionReceipts");

            migrationBuilder.DropColumn(
                name: "CoverJobDailyUsedOn",
                table: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropColumn(
                name: "JailUntilUtc",
                table: "AfterHoursPlayerCycleStates");
        }
    }
}
