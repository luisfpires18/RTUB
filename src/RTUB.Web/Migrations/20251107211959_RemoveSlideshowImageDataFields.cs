using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSlideshowImageDataFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // For SQLite, we need to recreate the table without the columns
            // This approach avoids PRAGMA statements that can't run in transactions
            
            // Create new table without ImageData and ImageContentType
            migrationBuilder.Sql(@"
                CREATE TABLE Slideshows_new (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    ImageUrl TEXT NOT NULL,
                    [Order] INTEGER NOT NULL,
                    IntervalMs INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    CreatedBy TEXT,
                    UpdatedBy TEXT
                );
            ");

            // Copy data from old table to new table
            migrationBuilder.Sql(@"
                INSERT INTO Slideshows_new (Id, Title, Description, ImageUrl, [Order], IntervalMs, IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                SELECT Id, Title, Description, ImageUrl, [Order], IntervalMs, IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
                FROM Slideshows;
            ");

            // Drop old table
            migrationBuilder.Sql("DROP TABLE Slideshows;");

            // Rename new table to original name
            migrationBuilder.Sql("ALTER TABLE Slideshows_new RENAME TO Slideshows;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "Slideshows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Slideshows",
                type: "BLOB",
                nullable: true);
        }
    }
}
