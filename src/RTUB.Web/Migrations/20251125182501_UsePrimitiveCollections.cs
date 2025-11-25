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

            // Rename CategoriesJson to Categories and preserve data
            // The existing JSON format is compatible with EF Core 10's primitive collections
            migrationBuilder.RenameColumn(
                name: "CategoriesJson",
                table: "AspNetUsers",
                newName: "Categories");
            
            // Rename PositionsJson to Positions and preserve data  
            // The existing JSON format is compatible with EF Core 10's primitive collections
            migrationBuilder.RenameColumn(
                name: "PositionsJson",
                table: "AspNetUsers",
                newName: "Positions");
            
            // Update null values to empty JSON arrays to satisfy IsRequired constraint
            migrationBuilder.Sql(
                "UPDATE AspNetUsers SET Categories = '[]' WHERE Categories IS NULL");
            migrationBuilder.Sql(
                "UPDATE AspNetUsers SET Positions = '[]' WHERE Positions IS NULL");

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

            // Rename back to original column names
            migrationBuilder.RenameColumn(
                name: "Categories",
                table: "AspNetUsers",
                newName: "CategoriesJson");
            
            migrationBuilder.RenameColumn(
                name: "Positions",
                table: "AspNetUsers",
                newName: "PositionsJson");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ActivityId",
                table: "Transactions",
                column: "ActivityId");
        }
    }
}
