using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddSurviveModeProgress : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SurviveModeProgresses",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                CurrentLevel = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                HighestLevel = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                CurrentRegion = table.Column<int>(type: "INTEGER", nullable: false),
                LongestSurvivalTime = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                TotalEnemiesKilled = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                TotalLevelsCompleted = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                TotalRunsAttempted = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                IsRunActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                RunStartLevel = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                RunStartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SurviveModeProgresses", x => x.Id);
                table.ForeignKey(
                    name: "FK_SurviveModeProgresses_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SurviveModeProgresses_HighestLevel",
            table: "SurviveModeProgresses",
            column: "HighestLevel");

        migrationBuilder.CreateIndex(
            name: "IX_SurviveModeProgresses_UserId",
            table: "SurviveModeProgresses",
            column: "UserId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SurviveModeProgresses");
    }
}
