using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddBossModeAndFitab : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FitabBalance",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BossModeProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CurrentBossStage = table.Column<int>(type: "INTEGER", nullable: false),
                    HighestBossStage = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalBossStagesCleared = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalFitabSpent = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalRunsAttempted = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BossModeProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BossModeProgresses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BossModeProgresses_HighestBossStage",
                table: "BossModeProgresses",
                column: "HighestBossStage");

            migrationBuilder.CreateIndex(
                name: "IX_BossModeProgresses_UserId",
                table: "BossModeProgresses",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BossModeProgresses");

            migrationBuilder.DropColumn(
                name: "FitabBalance",
                table: "AspNetUsers");
    }
}
