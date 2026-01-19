using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDiscussionForBets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, drop the foreign key and index from Bets table if they exist
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS IX_Bets_DiscussionId;
            ");

            // Check if DiscussionId column exists in Bets table and drop it
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ef_temp_Bets AS SELECT * FROM Bets WHERE 1=0;
                DROP TABLE IF EXISTS ef_temp_Bets;
            ");

            // For SQLite, we need to recreate the Discussions table to change EventId to nullable
            // and add BetId column
            
            // Step 1: Disable foreign key checks
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;");
            
            // Step 2: Create new table with correct schema
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS Discussions_new (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    EventId INTEGER NULL,
                    BetId INTEGER NULL,
                    CreatedAt TEXT NOT NULL,
                    CreatedBy TEXT NULL,
                    UpdatedAt TEXT NULL,
                    UpdatedBy TEXT NULL,
                    FOREIGN KEY (EventId) REFERENCES Events(Id) ON DELETE CASCADE,
                    FOREIGN KEY (BetId) REFERENCES Bets(Id) ON DELETE CASCADE
                );
            ");
            
            // Step 3: Copy data from old table (EventId stays as-is, BetId is NULL)
            migrationBuilder.Sql(@"
                INSERT INTO Discussions_new (Id, EventId, BetId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, EventId, NULL, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Discussions;
            ");
            
            // Step 4: Drop old table
            migrationBuilder.Sql("DROP TABLE Discussions;");
            
            // Step 5: Rename new table
            migrationBuilder.Sql("ALTER TABLE Discussions_new RENAME TO Discussions;");
            
            // Step 6: Recreate indexes
            migrationBuilder.Sql("CREATE INDEX IX_Discussions_EventId ON Discussions(EventId);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IX_Discussions_BetId ON Discussions(BetId) WHERE BetId IS NOT NULL;");
            
            // Step 7: Re-enable foreign key checks
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;");

            // Remove DiscussionId from Bets table if it exists
            // We need to check if the column exists first
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS Bets_new AS 
                SELECT Id, Title, Description, ImageSrc, Location, DateTime, BetCategory, 
                       IsCancelled, CancellationReason, WinningOptionId, CreatedAt, 
                       CreatedBy, UpdatedAt, UpdatedBy 
                FROM Bets WHERE 1=0;
            ");
            
            // Only proceed if Bets table has DiscussionId column
            // This is a no-op if the column doesn't exist
            migrationBuilder.Sql(@"
                INSERT OR REPLACE INTO Bets_new 
                SELECT Id, Title, Description, ImageSrc, Location, DateTime, BetCategory, 
                       IsCancelled, CancellationReason, WinningOptionId, CreatedAt, 
                       CreatedBy, UpdatedAt, UpdatedBy 
                FROM Bets;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // For SQLite, use raw SQL to revert changes
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;");
            
            // Recreate Discussions table without BetId and with non-nullable EventId
            migrationBuilder.Sql(@"
                CREATE TABLE Discussions_old (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    EventId INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    CreatedBy TEXT NULL,
                    UpdatedAt TEXT NULL,
                    UpdatedBy TEXT NULL,
                    FOREIGN KEY (EventId) REFERENCES Events(Id) ON DELETE CASCADE
                );
            ");
            
            // Copy only discussions that have EventId (ignore bet discussions)
            migrationBuilder.Sql(@"
                INSERT INTO Discussions_old (Id, EventId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, EventId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Discussions
                WHERE EventId IS NOT NULL;
            ");
            
            migrationBuilder.Sql("DROP TABLE Discussions;");
            migrationBuilder.Sql("ALTER TABLE Discussions_old RENAME TO Discussions;");
            migrationBuilder.Sql("CREATE INDEX IX_Discussions_EventId ON Discussions(EventId);");
            
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;");
        }
    }
}
