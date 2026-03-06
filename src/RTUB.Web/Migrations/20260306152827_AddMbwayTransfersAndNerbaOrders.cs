using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddMbwayTransfersAndNerbaOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MbwayTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    MemberUserId = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    FiscalYearId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MbwayTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MbwayTransfers_AspNetUsers_MemberUserId",
                        column: x => x.MemberUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MbwayTransfers_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NerbaOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Item = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Stock = table.Column<int>(type: "INTEGER", nullable: false),
                    PricePerUnit = table.Column<decimal>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReportId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NerbaOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NerbaOrders_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MbwayTransfers_Date",
                table: "MbwayTransfers",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_MbwayTransfers_FiscalYearId",
                table: "MbwayTransfers",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_MbwayTransfers_FiscalYearId_Date",
                table: "MbwayTransfers",
                columns: new[] { "FiscalYearId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_MbwayTransfers_MemberUserId",
                table: "MbwayTransfers",
                column: "MemberUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NerbaOrders_Date",
                table: "NerbaOrders",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_NerbaOrders_ReportId",
                table: "NerbaOrders",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_NerbaOrders_ReportId_Date",
                table: "NerbaOrders",
                columns: new[] { "ReportId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MbwayTransfers");

            migrationBuilder.DropTable(
                name: "NerbaOrders");
        }
    }
}
