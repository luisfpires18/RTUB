using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddAfterHoursPvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefenceOutfitKey",
                table: "AfterHoursPlayerCycleStates",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefenceTactic",
                table: "AfterHoursPlayerCycleStates",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenceVehicleToolKey",
                table: "AfterHoursPlayerCycleStates",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenceWeaponKey",
                table: "AfterHoursPlayerCycleStates",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PvpCooldownUntilUtc",
                table: "AfterHoursPlayerCycleStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PvpInitiatedAtUtc",
                table: "AfterHoursPlayerCycleStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PvpProtectedUntilUtc",
                table: "AfterHoursPlayerCycleStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PvpRecoveryUntilUtc",
                table: "AfterHoursPlayerCycleStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AfterHoursPvpBattles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameCycleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceiptId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerStateId = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderStateId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    DefenderUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AttackerTactic = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskStance = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderTactic = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderUsedSavedDefence = table.Column<bool>(type: "INTEGER", nullable: false),
                    AttackerWeaponKey = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    AttackerWeaponTier = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerOutfitKey = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    AttackerOutfitTier = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerVehicleToolKey = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    AttackerVehicleToolTier = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderWeaponKey = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    DefenderWeaponTier = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderOutfitKey = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    DefenderOutfitTier = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderVehicleToolKey = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    DefenderVehicleToolTier = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerEffectivePower = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderEffectivePower = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerLoadoutPower = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderLoadoutPower = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerSpecialisation = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderSpecialisation = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerMatchupBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderMatchupBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerRiskModifier = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerTotalDamage = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderTotalDamage = table.Column<int>(type: "INTEGER", nullable: false),
                    TieBreakRoll = table.Column<int>(type: "INTEGER", nullable: true),
                    AttackerWon = table.Column<bool>(type: "INTEGER", nullable: false),
                    LootMultiplier = table.Column<decimal>(type: "TEXT", nullable: false),
                    WalletStolen = table.Column<long>(type: "INTEGER", nullable: false),
                    AttackerCooldownUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AttackerRecoveryUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DefenderRecoveryUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DefenderProtectedUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndedAttackerNewPlayerProtection = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursPvpBattles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfterHoursPvpBattles_AfterHoursGameCycles_GameCycleId",
                        column: x => x.GameCycleId,
                        principalTable: "AfterHoursGameCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AfterHoursPvpBattles_AfterHoursPlayerActionReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "AfterHoursPlayerActionReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AfterHoursPvpBattles_AfterHoursPlayerCycleStates_AttackerStateId",
                        column: x => x.AttackerStateId,
                        principalTable: "AfterHoursPlayerCycleStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AfterHoursPvpBattles_AfterHoursPlayerCycleStates_DefenderStateId",
                        column: x => x.DefenderStateId,
                        principalTable: "AfterHoursPlayerCycleStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursPvpBattleCargo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PvpBattleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CargoType = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursPvpBattleCargo", x => x.Id);
                    table.CheckConstraint("CK_AfterHoursPvpBattleCargo_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_AfterHoursPvpBattleCargo_AfterHoursPvpBattles_PvpBattleId",
                        column: x => x.PvpBattleId,
                        principalTable: "AfterHoursPvpBattles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursPvpBattleRounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PvpBattleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerRandom = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderRandom = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerScore = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderScore = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackerDamage = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenderDamage = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursPvpBattleRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfterHoursPvpBattleRounds_AfterHoursPvpBattles_PvpBattleId",
                        column: x => x.PvpBattleId,
                        principalTable: "AfterHoursPvpBattles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPvpBattleCargo_PvpBattleId_CargoType",
                table: "AfterHoursPvpBattleCargo",
                columns: new[] { "PvpBattleId", "CargoType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPvpBattleRounds_PvpBattleId_Round",
                table: "AfterHoursPvpBattleRounds",
                columns: new[] { "PvpBattleId", "Round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPvpBattles_AttackerStateId_DefenderStateId_AcceptedAtUtc",
                table: "AfterHoursPvpBattles",
                columns: new[] { "AttackerStateId", "DefenderStateId", "AcceptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPvpBattles_DefenderStateId_AcceptedAtUtc",
                table: "AfterHoursPvpBattles",
                columns: new[] { "DefenderStateId", "AcceptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPvpBattles_GameCycleId",
                table: "AfterHoursPvpBattles",
                column: "GameCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPvpBattles_ReceiptId",
                table: "AfterHoursPvpBattles",
                column: "ReceiptId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AfterHoursPvpBattleCargo");

            migrationBuilder.DropTable(
                name: "AfterHoursPvpBattleRounds");

            migrationBuilder.DropTable(
                name: "AfterHoursPvpBattles");

            migrationBuilder.DropColumn(
                name: "DefenceOutfitKey",
                table: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropColumn(
                name: "DefenceTactic",
                table: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropColumn(
                name: "DefenceVehicleToolKey",
                table: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropColumn(
                name: "DefenceWeaponKey",
                table: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropColumn(
                name: "PvpCooldownUntilUtc",
                table: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropColumn(
                name: "PvpInitiatedAtUtc",
                table: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropColumn(
                name: "PvpProtectedUntilUtc",
                table: "AfterHoursPlayerCycleStates");

            migrationBuilder.DropColumn(
                name: "PvpRecoveryUntilUtc",
                table: "AfterHoursPlayerCycleStates");
        }
    }
}
