using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddAfterHoursAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AfterHoursCosmeticAwards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    RecipientName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    GrantedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GrantedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    FiscalYearId = table.Column<int>(type: "INTEGER", nullable: true),
                    CycleArchiveId = table.Column<int>(type: "INTEGER", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursCosmeticAwards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfterHoursCosmeticAwards_AfterHoursCycleArchives_CycleArchiveId",
                        column: x => x.CycleArchiveId,
                        principalTable: "AfterHoursCycleArchives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AfterHoursCosmeticAwards_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AfterHoursTuningSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfterHoursTuningSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursCosmeticAwards_CycleArchiveId",
                table: "AfterHoursCosmeticAwards",
                column: "CycleArchiveId");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursCosmeticAwards_FiscalYearId",
                table: "AfterHoursCosmeticAwards",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursCosmeticAwards_UserId_RevokedAtUtc",
                table: "AfterHoursCosmeticAwards",
                columns: new[] { "UserId", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AfterHoursTuningSettings_Key",
                table: "AfterHoursTuningSettings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AfterHoursCosmeticAwards");

            migrationBuilder.DropTable(
                name: "AfterHoursTuningSettings");
        }
    }
}
