using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class NerbaOrderEventRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete any NerbaOrders with NULL EventId — they can't satisfy the new FK constraint
            migrationBuilder.Sql("DELETE FROM NerbaOrders WHERE EventId IS NULL;");

            migrationBuilder.DropIndex(
                name: "IX_NerbaOrders_Date",
                table: "NerbaOrders");

            migrationBuilder.DropIndex(
                name: "IX_NerbaOrders_ReportId_Date",
                table: "NerbaOrders");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "NerbaOrders");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "NerbaOrders");

            migrationBuilder.AlterColumn<int>(
                name: "EventId",
                table: "NerbaOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "EventId",
                table: "NerbaOrders",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<DateTime>(
                name: "Date",
                table: "NerbaOrders",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "NerbaOrders",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NerbaOrders_Date",
                table: "NerbaOrders",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_NerbaOrders_ReportId_Date",
                table: "NerbaOrders",
                columns: new[] { "ReportId", "Date" });
        }
    }
}
