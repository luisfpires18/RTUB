using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddMemberDebtEntity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MemberDebts",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                AmountOwed = table.Column<decimal>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                FiscalYearId = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MemberDebts", x => x.Id);
                table.ForeignKey(
                    name: "FK_MemberDebts_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_MemberDebts_FiscalYears_FiscalYearId",
                    column: x => x.FiscalYearId,
                    principalTable: "FiscalYears",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MemberDebts_FiscalYearId",
            table: "MemberDebts",
            column: "FiscalYearId");

        migrationBuilder.CreateIndex(
            name: "IX_MemberDebts_UserId",
            table: "MemberDebts",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_MemberDebts_UserId_FiscalYearId",
            table: "MemberDebts",
            columns: new[] { "UserId", "FiscalYearId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MemberDebts");
    }
}
