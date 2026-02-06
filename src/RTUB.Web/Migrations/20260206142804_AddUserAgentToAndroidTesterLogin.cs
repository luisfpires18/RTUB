using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddUserAgentToAndroidTesterLogin : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "UserAgent",
            table: "AndroidTesterLogins",
            type: "TEXT",
            maxLength: 512,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UserAgent",
            table: "AndroidTesterLogins");
    }
}
