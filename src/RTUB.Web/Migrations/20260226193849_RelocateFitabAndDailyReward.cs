using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RelocateFitabAndDailyReward : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the new LastDailyRewardClaim column on Characters FIRST
            migrationBuilder.AddColumn<DateTime>(
                name: "LastDailyRewardClaim",
                table: "Characters",
                type: "TEXT",
                nullable: true);

            // 2. Migrate FitabBalance → InventoryItems (Type = 18 = Fitab)
            migrationBuilder.Sql("""
                INSERT INTO InventoryItems (UserId, Type, Quantity, CreatedAt, UpdatedAt)
                SELECT Id, 18, FitabBalance, datetime('now'), datetime('now')
                FROM AspNetUsers
                WHERE FitabBalance > 0
                """);

            // 3. Migrate LastDailyRewardClaim from AspNetUsers → Characters
            migrationBuilder.Sql("""
                UPDATE Characters
                SET LastDailyRewardClaim = (
                    SELECT LastDailyRewardClaim
                    FROM AspNetUsers
                    WHERE AspNetUsers.Id = Characters.UserId
                )
                """);

            // 4. Now safe to drop the old columns
            migrationBuilder.DropColumn(
                name: "FitabBalance",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastDailyRewardClaim",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add columns on AspNetUsers
            migrationBuilder.AddColumn<int>(
                name: "FitabBalance",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDailyRewardClaim",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            // Restore FitabBalance from InventoryItems (Type 18 = Fitab)
            migrationBuilder.Sql("""
                UPDATE AspNetUsers
                SET FitabBalance = COALESCE(
                    (SELECT Quantity FROM InventoryItems
                     WHERE InventoryItems.UserId = AspNetUsers.Id AND InventoryItems.Type = 18), 0)
                """);

            // Restore LastDailyRewardClaim from Characters
            migrationBuilder.Sql("""
                UPDATE AspNetUsers
                SET LastDailyRewardClaim = (
                    SELECT LastDailyRewardClaim FROM Characters
                    WHERE Characters.UserId = AspNetUsers.Id)
                """);

            // Remove the Fitab inventory rows (they'll be back on AspNetUsers)
            migrationBuilder.Sql("DELETE FROM InventoryItems WHERE Type = 18");

            migrationBuilder.DropColumn(
                name: "LastDailyRewardClaim",
                table: "Characters");
        }
    }
}
