using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class RemoveUniqueConstraintFromMemberDebts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_MemberDebts_UserId_FiscalYearId",
            table: "MemberDebts");

        migrationBuilder.CreateIndex(
            name: "IX_MemberDebts_UserId_FiscalYearId",
            table: "MemberDebts",
            columns: new[] { "UserId", "FiscalYearId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_MemberDebts_UserId_FiscalYearId",
            table: "MemberDebts");

        migrationBuilder.CreateIndex(
            name: "IX_MemberDebts_UserId_FiscalYearId",
            table: "MemberDebts",
            columns: new[] { "UserId", "FiscalYearId" },
            unique: true);
    }
}
