using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddAfterHoursFamilies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FamilyId",
                table: "AfterHoursPlayerActionReceipts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AfterHoursFamilies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Motto = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    DisbandedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursFamilies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursFamilyCycleStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FamilyId = table.Column<int>(type: "INTEGER", nullable: false),
                    GameCycleId = table.Column<int>(type: "INTEGER", nullable: false),
                    TreasuryCash = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursFamilyCycleStates", x => x.Id);
                    table.CheckConstraint("CK_AfterHoursFamilyCycleStates_Treasury", "\"TreasuryCash\" >= 0");
                    table.ForeignKey(
                        name: "FK_AfterHoursFamilyCycleStates_AfterHoursFamilies_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "AfterHoursFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AfterHoursFamilyCycleStates_AfterHoursGameCycles_GameCycleId",
                        column: x => x.GameCycleId,
                        principalTable: "AfterHoursGameCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursFamilyInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FamilyId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvitedUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    InvitedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursFamilyInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfterHoursFamilyInvitations_AfterHoursFamilies_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "AfterHoursFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AfterHoursFamilyInvitations_AspNetUsers_InvitedUserId",
                        column: x => x.InvitedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursFamilyMemberships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FamilyId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LeftAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursFamilyMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfterHoursFamilyMemberships_AfterHoursFamilies_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "AfterHoursFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AfterHoursFamilyMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursFamilies_NormalizedName",
                table: "AfterHoursFamilies",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursFamilyCycleStates_Family_Cycle",
                table: "AfterHoursFamilyCycleStates",
                columns: new[] { "FamilyId", "GameCycleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursFamilyCycleStates_GameCycleId",
                table: "AfterHoursFamilyCycleStates",
                column: "GameCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursFamilyInvitations_InvitedUserId_Status",
                table: "AfterHoursFamilyInvitations",
                columns: new[] { "InvitedUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursFamilyInvitations_PendingPerFamilyUser",
                table: "AfterHoursFamilyInvitations",
                columns: new[] { "FamilyId", "InvitedUserId" },
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursFamilyMemberships_ActiveBoss",
                table: "AfterHoursFamilyMemberships",
                column: "FamilyId",
                unique: true,
                filter: "\"LeftAtUtc\" IS NULL AND \"Role\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursFamilyMemberships_ActiveUser",
                table: "AfterHoursFamilyMemberships",
                column: "UserId",
                unique: true,
                filter: "\"LeftAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursFamilyMemberships_FamilyId_LeftAtUtc",
                table: "AfterHoursFamilyMemberships",
                columns: new[] { "FamilyId", "LeftAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursFamilyMemberships_UserId_LeftAtUtc",
                table: "AfterHoursFamilyMemberships",
                columns: new[] { "UserId", "LeftAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AfterHoursFamilyCycleStates");

            migrationBuilder.DropTable(
                name: "AfterHoursFamilyInvitations");

            migrationBuilder.DropTable(
                name: "AfterHoursFamilyMemberships");

            migrationBuilder.DropTable(
                name: "AfterHoursFamilies");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                table: "AfterHoursPlayerActionReceipts");
        }
    }
}
