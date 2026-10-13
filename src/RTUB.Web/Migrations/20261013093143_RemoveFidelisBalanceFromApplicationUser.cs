using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFidelisBalanceFromApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FidelisBalance",
                table: "AspNetUsers");
        }

        /// <summary>
        /// Schema-only, for local / DEV testing: adds the column back with 0 for every member, never the old balances,
        /// and does not restore the <c>PushNotificationsOptedOut</c> column default that Up's table rebuild drops. Not a
        /// production rollback path: restore the pre-migration database backup (docs/db-cleanup-inventory.md, Task 043).
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FidelisBalance",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
