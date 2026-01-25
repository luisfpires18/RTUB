using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddLogisticsCardAssignmentsAndReminders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LogisticsCardAssignments",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CardId = table.Column<int>(type: "INTEGER", nullable: false),
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LogisticsCardAssignments", x => x.Id);
                table.ForeignKey(
                    name: "FK_LogisticsCardAssignments_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_LogisticsCardAssignments_LogisticsCards_CardId",
                    column: x => x.CardId,
                    principalTable: "LogisticsCards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "LogisticsCardReminders",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CardId = table.Column<int>(type: "INTEGER", nullable: false),
                Frequency = table.Column<int>(type: "INTEGER", nullable: false),
                TargetUserIds = table.Column<string>(type: "TEXT", nullable: false),
                NextReminderDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                LastSentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LogisticsCardReminders", x => x.Id);
                table.ForeignKey(
                    name: "FK_LogisticsCardReminders_LogisticsCards_CardId",
                    column: x => x.CardId,
                    principalTable: "LogisticsCards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LogisticsCardAssignments_CardId",
            table: "LogisticsCardAssignments",
            column: "CardId");

        migrationBuilder.CreateIndex(
            name: "IX_LogisticsCardAssignments_UserId",
            table: "LogisticsCardAssignments",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_LogisticsCardReminders_CardId",
            table: "LogisticsCardReminders",
            column: "CardId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "LogisticsCardAssignments");

        migrationBuilder.DropTable(
            name: "LogisticsCardReminders");
    }
}
