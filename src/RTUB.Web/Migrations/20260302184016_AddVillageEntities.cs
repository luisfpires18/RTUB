using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddVillageEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Villages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Kingdom = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "Arkazia"),
                    PosX = table.Column<int>(type: "INTEGER", nullable: false),
                    PosY = table.Column<int>(type: "INTEGER", nullable: false),
                    LastTick = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Wood = table.Column<double>(type: "REAL", nullable: false),
                    Stone = table.Column<double>(type: "REAL", nullable: false),
                    Food = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Villages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Villages_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageBuildings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VillageId = table.Column<int>(type: "INTEGER", nullable: false),
                    BuildingType = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageBuildings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageBuildings_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageBuildJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VillageId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFieldUpgrade = table.Column<bool>(type: "INTEGER", nullable: false),
                    BuildingType = table.Column<int>(type: "INTEGER", nullable: true),
                    FieldResourceType = table.Column<int>(type: "INTEGER", nullable: true),
                    FieldSlotIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    ToLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationSecs = table.Column<int>(type: "INTEGER", nullable: false),
                    FinishesAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageBuildJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageBuildJobs_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VillageId = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    SlotIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageFields_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageMissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VillageId = table.Column<int>(type: "INTEGER", nullable: false),
                    MissionKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    SentTroopsJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishesAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RewardJson = table.Column<string>(type: "TEXT", nullable: true),
                    LossesJson = table.Column<string>(type: "TEXT", nullable: true),
                    Claimed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageMissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageMissions_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageTroops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VillageId = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitType = table.Column<int>(type: "INTEGER", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageTroops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageTroops_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VillageBuildings_Village_Type",
                table: "VillageBuildings",
                columns: new[] { "VillageId", "BuildingType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VillageBuildJobs_VillageId",
                table: "VillageBuildJobs",
                column: "VillageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VillageFields_Village_Resource_Slot",
                table: "VillageFields",
                columns: new[] { "VillageId", "ResourceType", "SlotIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VillageMissions_VillageId",
                table: "VillageMissions",
                column: "VillageId");

            migrationBuilder.CreateIndex(
                name: "IX_Villages_UserId",
                table: "Villages",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VillageTroops_Village_Unit",
                table: "VillageTroops",
                columns: new[] { "VillageId", "UnitType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VillageBuildings");

            migrationBuilder.DropTable(
                name: "VillageBuildJobs");

            migrationBuilder.DropTable(
                name: "VillageFields");

            migrationBuilder.DropTable(
                name: "VillageMissions");

            migrationBuilder.DropTable(
                name: "VillageTroops");

            migrationBuilder.DropTable(
                name: "Villages");
        }
    }
}
