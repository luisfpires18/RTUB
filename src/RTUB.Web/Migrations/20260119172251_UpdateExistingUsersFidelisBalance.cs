using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class UpdateExistingUsersFidelisBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update existing users who have FidelisBalance = 0 to 10
            // Using unquoted table name for SQLite compatibility
            migrationBuilder.Sql(@"
                UPDATE AspNetUsers
                SET FidelisBalance = 10
                WHERE FidelisBalance = 0
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
