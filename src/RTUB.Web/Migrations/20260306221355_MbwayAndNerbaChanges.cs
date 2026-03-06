using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class MbwayAndNerbaChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MbwayTransfers_AspNetUsers_MemberUserId",
                table: "MbwayTransfers");

            // Drop indexes only if they exist (database may not have them)
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_NerbaOrders_Date;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_NerbaOrders_ReportId_Date;");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "NerbaOrders");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "NerbaOrders");

            // Delete orphan NerbaOrders that have no EventId before adding the required FK
            migrationBuilder.Sql("DELETE FROM NerbaOrders;");

            migrationBuilder.AddColumn<int>(
                name: "EventId",
                table: "NerbaOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "NerbaOrders",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            // Delete MbwayTransfers with no member before making column required
            migrationBuilder.Sql("DELETE FROM MbwayTransfers WHERE MemberUserId IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "MemberUserId",
                table: "MbwayTransfers",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NerbaOrders_EventId",
                table: "NerbaOrders",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_MbwayTransfers_AspNetUsers_MemberUserId",
                table: "MbwayTransfers",
                column: "MemberUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NerbaOrders_Events_EventId",
                table: "NerbaOrders",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MbwayTransfers_AspNetUsers_MemberUserId",
                table: "MbwayTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_NerbaOrders_Events_EventId",
                table: "NerbaOrders");

            migrationBuilder.DropIndex(
                name: "IX_NerbaOrders_EventId",
                table: "NerbaOrders");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "NerbaOrders");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "NerbaOrders");

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

            migrationBuilder.AlterColumn<string>(
                name: "MemberUserId",
                table: "MbwayTransfers",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.CreateIndex(
                name: "IX_NerbaOrders_Date",
                table: "NerbaOrders",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_NerbaOrders_ReportId_Date",
                table: "NerbaOrders",
                columns: new[] { "ReportId", "Date" });

            migrationBuilder.AddForeignKey(
                name: "FK_MbwayTransfers_AspNetUsers_MemberUserId",
                table: "MbwayTransfers",
                column: "MemberUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
