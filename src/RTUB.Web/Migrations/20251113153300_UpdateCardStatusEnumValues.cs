using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCardStatusEnumValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update existing cards with Status = 1 (was Done, now InProgress) to Status = 2 (new Done value)
            // This migration ensures backward compatibility when we added InProgress status
            migrationBuilder.Sql(
                "UPDATE LogisticsCards SET Status = 2 WHERE Status = 1;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert: Change Done (2) back to old Done value (1)
            migrationBuilder.Sql(
                "UPDATE LogisticsCards SET Status = 1 WHERE Status = 2;"
            );
        }
    }
}
