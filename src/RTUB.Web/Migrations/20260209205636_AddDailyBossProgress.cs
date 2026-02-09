using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddDailyBossProgress : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DailyBossMaxHP",
            table: "BossModeProgresses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "DailyBossRemainingHP",
            table: "BossModeProgresses",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DailyBossStage",
            table: "BossModeProgresses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastDailyResetDate",
            table: "BossModeProgresses",
            type: "TEXT",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DailyBossMaxHP",
            table: "BossModeProgresses");

        migrationBuilder.DropColumn(
            name: "DailyBossRemainingHP",
            table: "BossModeProgresses");

        migrationBuilder.DropColumn(
            name: "DailyBossStage",
            table: "BossModeProgresses");

        migrationBuilder.DropColumn(
            name: "LastDailyResetDate",
            table: "BossModeProgresses");
    }
}
