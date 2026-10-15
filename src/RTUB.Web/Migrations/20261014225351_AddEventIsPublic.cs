using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddEventIsPublic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every existing event stays public, and so does any event the previous release (N-1, which
            // does not know this column) creates after an app rollback: the column defaults to 1. The model
            // has no default on purpose, so EF always writes the value the app chose (false included).
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Events");
        }
    }
}
