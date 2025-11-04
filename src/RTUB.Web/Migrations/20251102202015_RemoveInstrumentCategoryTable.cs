using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInstrumentCategoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite requires table rebuild for foreign key changes
            // Step 1: Create new Instruments table without foreign key
            migrationBuilder.Sql(@"
                CREATE TABLE ""Instruments_new"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Instruments"" PRIMARY KEY AUTOINCREMENT,
                    ""Category"" TEXT NOT NULL DEFAULT '',
                    ""Name"" TEXT NOT NULL,
                    ""SerialNumber"" TEXT NULL,
                    ""Brand"" TEXT NULL,
                    ""Condition"" INTEGER NOT NULL,
                    ""PurchaseDate"" TEXT NULL,
                    ""PurchasePrice"" TEXT NULL,
                    ""Location"" TEXT NULL,
                    ""MaintenanceNotes"" TEXT NULL,
                    ""LastMaintenanceDate"" TEXT NULL,
                    ""ImageData"" BLOB NULL,
                    ""ImageContentType"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""UpdatedBy"" TEXT NULL
                );
            ");

            // Step 2: Copy data from old table to new table (if old table exists)
            migrationBuilder.Sql(@"
                INSERT INTO ""Instruments_new""
                    (""Id"", ""Name"", ""SerialNumber"", ""Brand"", ""Condition"", ""PurchaseDate"",
                     ""PurchasePrice"", ""Location"", ""MaintenanceNotes"", ""LastMaintenanceDate"",
                     ""ImageData"", ""ImageContentType"", ""CreatedAt"", ""CreatedBy"", ""UpdatedAt"", ""UpdatedBy"", ""Category"")
                SELECT
                    ""Id"", ""Name"", ""SerialNumber"", ""Brand"", ""Condition"", ""PurchaseDate"",
                    ""PurchasePrice"", ""Location"", ""MaintenanceNotes"", ""LastMaintenanceDate"",
                    ""ImageData"", ""ImageContentType"", ""CreatedAt"", ""CreatedBy"", ""UpdatedAt"", ""UpdatedBy"",
                    COALESCE((SELECT ""Name"" FROM ""InstrumentCategories"" WHERE ""Id"" = ""Instruments"".""InstrumentCategoryId""), '') as ""Category""
                FROM ""Instruments""
                WHERE EXISTS (SELECT 1 FROM sqlite_master WHERE type='table' AND name='Instruments');
            ");

            // Step 3: Drop old table
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Instruments"";");

            // Step 4: Rename new table
            migrationBuilder.Sql(@"ALTER TABLE ""Instruments_new"" RENAME TO ""Instruments"";");

            // Step 5: Drop InstrumentCategories table
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""InstrumentCategories"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate InstrumentCategories table
            migrationBuilder.CreateTable(
                name: "InstrumentCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentCategories", x => x.Id);
                });

            // Rebuild Instruments table with foreign key
            migrationBuilder.Sql(@"
                CREATE TABLE ""Instruments_old"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Instruments"" PRIMARY KEY AUTOINCREMENT,
                    ""InstrumentCategoryId"" INTEGER NOT NULL DEFAULT 0,
                    ""Name"" TEXT NOT NULL,
                    ""SerialNumber"" TEXT NULL,
                    ""Brand"" TEXT NULL,
                    ""Condition"" INTEGER NOT NULL,
                    ""PurchaseDate"" TEXT NULL,
                    ""PurchasePrice"" TEXT NULL,
                    ""Location"" TEXT NULL,
                    ""MaintenanceNotes"" TEXT NULL,
                    ""LastMaintenanceDate"" TEXT NULL,
                    ""ImageData"" BLOB NULL,
                    ""ImageContentType"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""UpdatedBy"" TEXT NULL,
                    FOREIGN KEY(""InstrumentCategoryId"") REFERENCES ""InstrumentCategories""(""Id"") ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Instruments_old""
                    (""Id"", ""Name"", ""SerialNumber"", ""Brand"", ""Condition"", ""PurchaseDate"",
                     ""PurchasePrice"", ""Location"", ""MaintenanceNotes"", ""LastMaintenanceDate"",
                     ""ImageData"", ""ImageContentType"", ""CreatedAt"", ""CreatedBy"", ""UpdatedAt"", ""UpdatedBy"", ""InstrumentCategoryId"")
                SELECT
                    ""Id"", ""Name"", ""SerialNumber"", ""Brand"", ""Condition"", ""PurchaseDate"",
                    ""PurchasePrice"", ""Location"", ""MaintenanceNotes"", ""LastMaintenanceDate"",
                    ""ImageData"", ""ImageContentType"", ""CreatedAt"", ""CreatedBy"", ""UpdatedAt"", ""UpdatedBy"",
                    0 as ""InstrumentCategoryId""
                FROM ""Instruments"";
            ");

            migrationBuilder.Sql(@"DROP TABLE ""Instruments"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Instruments_old"" RENAME TO ""Instruments"";");

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_InstrumentCategoryId",
                table: "Instruments",
                column: "InstrumentCategoryId");
        }
    }
}
