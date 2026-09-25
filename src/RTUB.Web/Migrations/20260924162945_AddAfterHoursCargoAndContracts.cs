using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddAfterHoursCargoAndContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CargoDelta",
                table: "AfterHoursPlayerActionReceipts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CargoType",
                table: "AfterHoursPlayerActionReceipts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AfterHoursBuyerContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameCycleId = table.Column<int>(type: "INTEGER", nullable: false),
                    RotationStartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    TemplateKey = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BuyerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CargoType = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    CashReward = table.Column<long>(type: "INTEGER", nullable: false),
                    XpReward = table.Column<long>(type: "INTEGER", nullable: false),
                    AvailableFromUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursBuyerContracts", x => x.Id);
                    table.CheckConstraint("CK_AfterHoursBuyerContracts_Window", "\"ExpiresAtUtc\" > \"AvailableFromUtc\"");
                    table.ForeignKey(
                        name: "FK_AfterHoursBuyerContracts_AfterHoursGameCycles_GameCycleId",
                        column: x => x.GameCycleId,
                        principalTable: "AfterHoursGameCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursPlayerCargo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerCycleStateId = table.Column<int>(type: "INTEGER", nullable: false),
                    CargoType = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursPlayerCargo", x => x.Id);
                    table.CheckConstraint("CK_AfterHoursPlayerCargo_Quantity", "\"Quantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_AfterHoursPlayerCargo_AfterHoursPlayerCycleStates_PlayerCycleStateId",
                        column: x => x.PlayerCycleStateId,
                        principalTable: "AfterHoursPlayerCycleStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursBuyerContractCompletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuyerContractId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerCycleStateId = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursBuyerContractCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfterHoursBuyerContractCompletions_AfterHoursBuyerContracts_BuyerContractId",
                        column: x => x.BuyerContractId,
                        principalTable: "AfterHoursBuyerContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AfterHoursBuyerContractCompletions_AfterHoursPlayerCycleStates_PlayerCycleStateId",
                        column: x => x.PlayerCycleStateId,
                        principalTable: "AfterHoursPlayerCycleStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursBuyerContractCompletions_Contract_State",
                table: "AfterHoursBuyerContractCompletions",
                columns: new[] { "BuyerContractId", "PlayerCycleStateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursBuyerContractCompletions_PlayerCycleStateId",
                table: "AfterHoursBuyerContractCompletions",
                column: "PlayerCycleStateId");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursBuyerContracts_Cycle_Window_Slot",
                table: "AfterHoursBuyerContracts",
                columns: new[] { "GameCycleId", "RotationStartUtc", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursPlayerCargo_State_Type",
                table: "AfterHoursPlayerCargo",
                columns: new[] { "PlayerCycleStateId", "CargoType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AfterHoursBuyerContractCompletions");

            migrationBuilder.DropTable(
                name: "AfterHoursPlayerCargo");

            migrationBuilder.DropTable(
                name: "AfterHoursBuyerContracts");

            migrationBuilder.DropColumn(
                name: "CargoDelta",
                table: "AfterHoursPlayerActionReceipts");

            migrationBuilder.DropColumn(
                name: "CargoType",
                table: "AfterHoursPlayerActionReceipts");
        }
    }
}
