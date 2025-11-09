using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAlbumAndInstrumentBlobFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ========================================
            // Remove BLOB fields from Albums table
            // ========================================
            
            // For SQLite, we need to recreate the table without the columns
            // This approach avoids PRAGMA statements that can't run in transactions
            
            // Create new Albums table without CoverImageData and CoverImageContentType
            migrationBuilder.Sql(@"
                CREATE TABLE Albums_new (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT,
                    Year INTEGER,
                    CoverImageUrl TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    CreatedBy TEXT,
                    UpdatedBy TEXT
                );
            ");

            // Copy data from old table to new table
            migrationBuilder.Sql(@"
                INSERT INTO Albums_new (Id, Title, Description, Year, CoverImageUrl, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                SELECT Id, Title, Description, Year, CoverImageUrl, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
                FROM Albums;
            ");

            // Drop old table
            migrationBuilder.Sql("DROP TABLE Albums;");

            // Rename new table to original name
            migrationBuilder.Sql("ALTER TABLE Albums_new RENAME TO Albums;");

            // ========================================
            // Remove BLOB fields from Instruments table
            // ========================================
            
            // Create new Instruments table without ImageData and ImageContentType
            migrationBuilder.Sql(@"
                CREATE TABLE Instruments_new (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Category TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    SerialNumber TEXT,
                    Brand TEXT,
                    Condition INTEGER NOT NULL,
                    Location TEXT,
                    MaintenanceNotes TEXT,
                    LastMaintenanceDate TEXT,
                    ImageUrl TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    CreatedBy TEXT,
                    UpdatedBy TEXT
                );
            ");

            // Copy data from old table to new table
            migrationBuilder.Sql(@"
                INSERT INTO Instruments_new (Id, Category, Name, SerialNumber, Brand, Condition, Location, MaintenanceNotes, LastMaintenanceDate, ImageUrl, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                SELECT Id, Category, Name, SerialNumber, Brand, Condition, Location, MaintenanceNotes, LastMaintenanceDate, ImageUrl, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
                FROM Instruments;
            ");

            // Drop old table
            migrationBuilder.Sql("DROP TABLE Instruments;");

            // Rename new table to original name
            migrationBuilder.Sql("ALTER TABLE Instruments_new RENAME TO Instruments;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add back BLOB fields to Albums
            migrationBuilder.AddColumn<string>(
                name: "CoverImageContentType",
                table: "Albums",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "CoverImageData",
                table: "Albums",
                type: "BLOB",
                nullable: true);

            // Add back BLOB fields to Instruments
            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "Instruments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Instruments",
                type: "BLOB",
                nullable: true);
        }
    }
}
