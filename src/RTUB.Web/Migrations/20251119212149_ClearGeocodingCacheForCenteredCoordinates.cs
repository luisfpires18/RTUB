using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class ClearGeocodingCacheForCenteredCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Meetings_Date",
                table: "Meetings",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Date",
                table: "Events",
                column: "Date");

            // Clear the geocoding cache to force regeneration with centered coordinates
            // This ensures all cities use the new bounding box center calculation
            migrationBuilder.Sql("DELETE FROM GeocodingCaches;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Meetings_Date",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_Events_Date",
                table: "Events");
        }
    }
}
