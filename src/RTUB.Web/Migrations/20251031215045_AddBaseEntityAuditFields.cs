using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddBaseEntityAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "SongYouTubeUrls",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SongYouTubeUrls",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "SongYouTubeUrls",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Songs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Songs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Songs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Slideshows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Slideshows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Slideshows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RoleAssignments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "RoleAssignments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Reports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Reports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Reports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "RehearsalSchedules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RehearsalSchedules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "RehearsalSchedules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Rehearsals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Rehearsals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Rehearsals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "RehearsalAttendances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RehearsalAttendances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "RehearsalAttendances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Products",
                type: "TEXT",
                nullable: true);

            // Manually rebuild Labels table to change UpdatedAt to nullable and add audit fields
            // Using raw SQL to avoid EF Core's automatic PRAGMA generation within transactions
            migrationBuilder.Sql(@"
                CREATE TABLE ""Labels_new"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Labels"" PRIMARY KEY AUTOINCREMENT,
                    ""Content"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""IsActive"" INTEGER NOT NULL,
                    ""Reference"" TEXT NULL,
                    ""Title"" TEXT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""UpdatedBy"" TEXT NULL
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Labels_new""
                    (""Id"", ""Content"", ""CreatedAt"", ""IsActive"", ""Reference"", ""Title"", ""UpdatedAt"", ""CreatedBy"", ""UpdatedBy"")
                SELECT
                    ""Id"", ""Content"", ""CreatedAt"", ""IsActive"", ""Reference"", ""Title"", ""UpdatedAt"", NULL, NULL
                FROM ""Labels"";
            ");

            migrationBuilder.Sql(@"DROP TABLE ""Labels"";");

            migrationBuilder.Sql(@"ALTER TABLE ""Labels_new"" RENAME TO ""Labels"";");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Instruments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Instruments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Instruments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "InstrumentCategories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "InstrumentCategories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "InstrumentCategories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "FiscalYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "FiscalYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "FiscalYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "EventRepertoires",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "EventRepertoires",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "EventRepertoires",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Enrollments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Enrollments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Enrollments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Albums",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Albums",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Albums",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Activities",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SongYouTubeUrls");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SongYouTubeUrls");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "SongYouTubeUrls");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Slideshows");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Slideshows");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Slideshows");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "RoleAssignments");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "RoleAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "RehearsalSchedules");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "RehearsalSchedules");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "RehearsalSchedules");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Rehearsals");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Rehearsals");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Rehearsals");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "RehearsalAttendances");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "RehearsalAttendances");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "RehearsalAttendances");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Labels");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Labels");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "InstrumentCategories");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "InstrumentCategories");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "InstrumentCategories");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "EventRepertoires");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "EventRepertoires");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "EventRepertoires");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Activities");

            // Manually rebuild Labels table to revert UpdatedAt to non-nullable and remove audit fields
            // Using raw SQL to avoid EF Core's automatic PRAGMA generation within transactions
            migrationBuilder.Sql(@"
                CREATE TABLE ""Labels_old"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Labels"" PRIMARY KEY AUTOINCREMENT,
                    ""Content"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""IsActive"" INTEGER NOT NULL,
                    ""Reference"" TEXT NULL,
                    ""Title"" TEXT NULL,
                    ""UpdatedAt"" TEXT NOT NULL
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Labels_old""
                    (""Id"", ""Content"", ""CreatedAt"", ""IsActive"", ""Reference"", ""Title"", ""UpdatedAt"")
                SELECT
                    ""Id"", ""Content"", ""CreatedAt"", ""IsActive"", ""Reference"", ""Title"", 
                    COALESCE(""UpdatedAt"", '0001-01-01 00:00:00')
                FROM ""Labels"";
            ");

            migrationBuilder.Sql(@"DROP TABLE ""Labels"";");

            migrationBuilder.Sql(@"ALTER TABLE ""Labels_old"" RENAME TO ""Labels"";");
        }
    }
}
