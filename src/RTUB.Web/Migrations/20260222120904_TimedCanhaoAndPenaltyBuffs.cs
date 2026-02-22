using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class TimedCanhaoAndPenaltyBuffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanhaoDamageBoostHitsRemaining",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PenaltyBuffActive",
                table: "Characters");

            migrationBuilder.AddColumn<DateTime>(
                name: "CanhaoBuffExpiresAt",
                table: "Characters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PenaltyBuffExpiresAt",
                table: "Characters",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanhaoBuffExpiresAt",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PenaltyBuffExpiresAt",
                table: "Characters");

            migrationBuilder.AddColumn<int>(
                name: "CanhaoDamageBoostHitsRemaining",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PenaltyBuffActive",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
