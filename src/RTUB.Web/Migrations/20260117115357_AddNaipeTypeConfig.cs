using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddNaipeTypeConfig : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NaipeTypeConfigs",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                InstrumentType = table.Column<int>(type: "INTEGER", nullable: false),
                PictureUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                IsVisible = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NaipeTypeConfigs", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_NaipeTypeConfigs_InstrumentType",
            table: "NaipeTypeConfigs",
            column: "InstrumentType",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_NaipeTypeConfigs_SortOrder",
            table: "NaipeTypeConfigs",
            column: "SortOrder");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "NaipeTypeConfigs");
    }
}
