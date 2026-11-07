using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddLogisticsReminderDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                table: "LogisticsCardReminders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "LogisticsCardReminders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedUntil",
                table: "LogisticsCardReminders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "LogisticsCardReminders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Personal",
                table: "LogisticsCardReminders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "LogisticsCardReminders");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "LogisticsCardReminders");

            migrationBuilder.DropColumn(
                name: "ClaimedUntil",
                table: "LogisticsCardReminders");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "LogisticsCardReminders");

            migrationBuilder.DropColumn(
                name: "Personal",
                table: "LogisticsCardReminders");
        }
    }
}
