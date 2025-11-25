using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class UsePrimitiveCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_ActivityId",
                table: "Transactions");

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

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ActivityId_Type",
                table: "Transactions",
                columns: new[] { "ActivityId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Date",
                table: "Transactions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_EventId_UserId",
                table: "Enrollments",
                columns: new[] { "EventId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_IsCriticalAction",
                table: "AuditLogs",
                column: "IsCriticalAction");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserName",
                table: "AuditLogs",
                column: "UserName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_ActivityId_Type",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_Date",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_EventId_UserId",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_IsCriticalAction",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserName",
                table: "AuditLogs");

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

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ActivityId",
                table: "Transactions",
                column: "ActivityId");
        }
    }
}
