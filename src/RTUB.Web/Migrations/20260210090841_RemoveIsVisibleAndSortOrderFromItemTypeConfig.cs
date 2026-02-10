using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class RemoveIsVisibleAndSortOrderFromItemTypeConfig : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsVisible",
            table: "ItemTypeConfigs");

        migrationBuilder.DropColumn(
            name: "SortOrder",
            table: "ItemTypeConfigs");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsVisible",
            table: "ItemTypeConfigs",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "SortOrder",
            table: "ItemTypeConfigs",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }
}
