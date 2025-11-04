using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class MakeAlbumYearNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite doesn't support altering columns directly, so we need to recreate the table
            // Temporarily disable foreign key constraints
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;");
            
            // First, create a temporary table with the new schema
            migrationBuilder.Sql(@"
                CREATE TABLE Albums_temp (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT NULL,
                    Year INTEGER NULL,
                    CoverImageData BLOB NULL,
                    CoverImageContentType TEXT NULL,
                    CoverImageUrl TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    CreatedBy TEXT NULL,
                    UpdatedAt TEXT NULL,
                    UpdatedBy TEXT NULL
                );
            ");

            // Copy data from old table to new table
            migrationBuilder.Sql(@"
                INSERT INTO Albums_temp (Id, Title, Description, Year, CoverImageData, CoverImageContentType, CoverImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, Title, Description, Year, CoverImageData, CoverImageContentType, CoverImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Albums;
            ");

            // Drop the old table
            migrationBuilder.Sql("DROP TABLE Albums;");

            // Rename the temp table to the original name
            migrationBuilder.Sql("ALTER TABLE Albums_temp RENAME TO Albums;");
            
            // Re-enable foreign key constraints
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert by making Year NOT NULL again
            // Temporarily disable foreign key constraints
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;");
            
            migrationBuilder.Sql(@"
                CREATE TABLE Albums_temp (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT NULL,
                    Year INTEGER NOT NULL,
                    CoverImageData BLOB NULL,
                    CoverImageContentType TEXT NULL,
                    CoverImageUrl TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    CreatedBy TEXT NULL,
                    UpdatedAt TEXT NULL,
                    UpdatedBy TEXT NULL
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO Albums_temp (Id, Title, Description, Year, CoverImageData, CoverImageContentType, CoverImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, Title, Description, COALESCE(Year, 0), CoverImageData, CoverImageContentType, CoverImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Albums;
            ");

            migrationBuilder.Sql("DROP TABLE Albums;");
            migrationBuilder.Sql("ALTER TABLE Albums_temp RENAME TO Albums;");
            
            // Re-enable foreign key constraints
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;");
        }
    }
}
