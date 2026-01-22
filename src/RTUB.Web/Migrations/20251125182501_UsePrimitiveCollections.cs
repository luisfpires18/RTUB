using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class UsePrimitiveCollections : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Migrate data from CategoriesJson to Categories column
        // The existing JSON format is compatible with EF Core 10's primitive collections
        migrationBuilder.Sql(
            "UPDATE AspNetUsers SET Categories = COALESCE(CategoriesJson, '[]') WHERE CategoriesJson IS NOT NULL");

        // Migrate data from PositionsJson to Positions column
        migrationBuilder.Sql(
            "UPDATE AspNetUsers SET Positions = COALESCE(PositionsJson, '[]') WHERE PositionsJson IS NOT NULL");

        // Update null values to empty JSON arrays to satisfy IsRequired constraint
        migrationBuilder.Sql(
            "UPDATE AspNetUsers SET Categories = '[]' WHERE Categories IS NULL OR Categories = ''");
        migrationBuilder.Sql(
            "UPDATE AspNetUsers SET Positions = '[]' WHERE Positions IS NULL OR Positions = ''");

        // Drop the old JSON columns - they're no longer needed
        migrationBuilder.DropColumn(
            name: "CategoriesJson",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "PositionsJson",
            table: "AspNetUsers");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Recreate the JSON columns
        migrationBuilder.AddColumn<string>(
            name: "CategoriesJson",
            table: "AspNetUsers",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PositionsJson",
            table: "AspNetUsers",
            type: "TEXT",
            nullable: true);

        // Migrate data back to JSON columns
        migrationBuilder.Sql(
            "UPDATE AspNetUsers SET CategoriesJson = Categories WHERE Categories != '[]'");
        migrationBuilder.Sql(
            "UPDATE AspNetUsers SET PositionsJson = Positions WHERE Positions != '[]'");
    }
}
