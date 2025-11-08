using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEventImageDataFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // For SQLite, we need to recreate the table without the columns
            // This approach avoids PRAGMA statements that can't run in transactions
            
            // Create new table without ImageData and ImageContentType
            migrationBuilder.Sql(@"
                CREATE TABLE Events_new (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Date TEXT NOT NULL,
                    EndDate TEXT,
                    Description TEXT,
                    Location TEXT NOT NULL,
                    Type INTEGER NOT NULL,
                    ImageUrl TEXT,
                    S3ImageFilename TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT
                );
            ");

            // Copy data from old table to new table
            migrationBuilder.Sql(@"
                INSERT INTO Events_new (Id, Name, Date, EndDate, Description, Location, Type, ImageUrl, S3ImageFilename, CreatedAt, UpdatedAt)
                SELECT Id, Name, Date, EndDate, Description, Location, Type, ImageUrl, S3ImageFilename, CreatedAt, UpdatedAt
                FROM Events;
            ");

            // Drop old table
            migrationBuilder.Sql("DROP TABLE Events;");

            // Rename new table to original name
            migrationBuilder.Sql("ALTER TABLE Events_new RENAME TO Events;");

            // Note: Foreign keys are defined on the referencing tables (Enrollments, EventRepertoire, Trophy)
            // not on the Events table itself, so they don't need to be recreated here
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "Events",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Events",
                type: "BLOB",
                nullable: true);
        }
    }
}
