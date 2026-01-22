using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class MakeFirstSecretaryOptional : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQLite doesn't support ALTER COLUMN directly, so we need to recreate the table

        // Create a new table with the correct schema (FirstSecretaryUserId is now nullable)
        migrationBuilder.Sql(@"
                CREATE TABLE ""MeetingAtas_new"" (
                    ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    ""MeetingId"" INTEGER NOT NULL,
                    ""AtaNumber"" TEXT NULL,
                    ""ActualStartTime"" TEXT NOT NULL,
                    ""ActualEndTime"" TEXT NULL,
                    ""Location"" TEXT NOT NULL,
                    ""PresidentUserId"" TEXT NOT NULL,
                    ""FirstSecretaryUserId"" TEXT NULL,
                    ""SecondSecretaryUserId"" TEXT NULL,
                    ""QuorumBasis"" TEXT NOT NULL,
                    ""AttendeesPresent"" TEXT NOT NULL,
                    ""AttendeesAbsent"" TEXT NOT NULL,
                    ""ClosingText"" TEXT NULL,
                    ""Status"" INTEGER NOT NULL,
                    ""GeneratedAt"" TEXT NULL,
                    ""PdfStorageUrl"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""UpdatedBy"" TEXT NULL,
                    CONSTRAINT ""FK_MeetingAtas_AspNetUsers_FirstSecretaryUserId"" FOREIGN KEY (""FirstSecretaryUserId"") REFERENCES ""AspNetUsers"" (""Id""),
                    CONSTRAINT ""FK_MeetingAtas_AspNetUsers_PresidentUserId"" FOREIGN KEY (""PresidentUserId"") REFERENCES ""AspNetUsers"" (""Id""),
                    CONSTRAINT ""FK_MeetingAtas_AspNetUsers_SecondSecretaryUserId"" FOREIGN KEY (""SecondSecretaryUserId"") REFERENCES ""AspNetUsers"" (""Id""),
                    CONSTRAINT ""FK_MeetingAtas_Meetings_MeetingId"" FOREIGN KEY (""MeetingId"") REFERENCES ""Meetings"" (""Id"") ON DELETE CASCADE
                );
            ");

        // Copy data from old table
        migrationBuilder.Sql(@"
                INSERT INTO ""MeetingAtas_new"" 
                SELECT * FROM ""MeetingAtas"";
            ");

        // Drop old table
        migrationBuilder.Sql(@"DROP TABLE ""MeetingAtas"";");

        // Rename new table
        migrationBuilder.Sql(@"ALTER TABLE ""MeetingAtas_new"" RENAME TO ""MeetingAtas"";");

        // Recreate indexes
        migrationBuilder.Sql(@"CREATE INDEX ""IX_MeetingAtas_FirstSecretaryUserId"" ON ""MeetingAtas"" (""FirstSecretaryUserId"");");
        migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""IX_MeetingAtas_MeetingId"" ON ""MeetingAtas"" (""MeetingId"");");
        migrationBuilder.Sql(@"CREATE INDEX ""IX_MeetingAtas_PresidentUserId"" ON ""MeetingAtas"" (""PresidentUserId"");");
        migrationBuilder.Sql(@"CREATE INDEX ""IX_MeetingAtas_SecondSecretaryUserId"" ON ""MeetingAtas"" (""SecondSecretaryUserId"");");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Create old table with NOT NULL constraint on FirstSecretaryUserId
        migrationBuilder.Sql(@"
                CREATE TABLE ""MeetingAtas_old"" (
                    ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    ""MeetingId"" INTEGER NOT NULL,
                    ""AtaNumber"" TEXT NULL,
                    ""ActualStartTime"" TEXT NOT NULL,
                    ""ActualEndTime"" TEXT NULL,
                    ""Location"" TEXT NOT NULL,
                    ""PresidentUserId"" TEXT NOT NULL,
                    ""FirstSecretaryUserId"" TEXT NOT NULL,
                    ""SecondSecretaryUserId"" TEXT NULL,
                    ""QuorumBasis"" TEXT NOT NULL,
                    ""AttendeesPresent"" TEXT NOT NULL,
                    ""AttendeesAbsent"" TEXT NOT NULL,
                    ""ClosingText"" TEXT NULL,
                    ""Status"" INTEGER NOT NULL,
                    ""GeneratedAt"" TEXT NULL,
                    ""PdfStorageUrl"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""UpdatedBy"" TEXT NULL,
                    CONSTRAINT ""FK_MeetingAtas_AspNetUsers_FirstSecretaryUserId"" FOREIGN KEY (""FirstSecretaryUserId"") REFERENCES ""AspNetUsers"" (""Id""),
                    CONSTRAINT ""FK_MeetingAtas_AspNetUsers_PresidentUserId"" FOREIGN KEY (""PresidentUserId"") REFERENCES ""AspNetUsers"" (""Id""),
                    CONSTRAINT ""FK_MeetingAtas_AspNetUsers_SecondSecretaryUserId"" FOREIGN KEY (""SecondSecretaryUserId"") REFERENCES ""AspNetUsers"" (""Id""),
                    CONSTRAINT ""FK_MeetingAtas_Meetings_MeetingId"" FOREIGN KEY (""MeetingId"") REFERENCES ""Meetings"" (""Id"") ON DELETE CASCADE
                );
            ");

        // Copy data - use PresidentUserId as fallback for NULL FirstSecretaryUserId values
        migrationBuilder.Sql(@"
                INSERT INTO ""MeetingAtas_old"" 
                SELECT ""Id"", ""MeetingId"", ""AtaNumber"", ""ActualStartTime"", ""ActualEndTime"", ""Location"", 
                       ""PresidentUserId"", COALESCE(""FirstSecretaryUserId"", ""PresidentUserId""), ""SecondSecretaryUserId"",
                       ""QuorumBasis"", ""AttendeesPresent"", ""AttendeesAbsent"", ""ClosingText"", ""Status"",
                       ""GeneratedAt"", ""PdfStorageUrl"", ""CreatedAt"", ""CreatedBy"", ""UpdatedAt"", ""UpdatedBy""
                FROM ""MeetingAtas"";
            ");

        migrationBuilder.Sql(@"DROP TABLE ""MeetingAtas"";");
        migrationBuilder.Sql(@"ALTER TABLE ""MeetingAtas_old"" RENAME TO ""MeetingAtas"";");

        // Recreate indexes
        migrationBuilder.Sql(@"CREATE INDEX ""IX_MeetingAtas_FirstSecretaryUserId"" ON ""MeetingAtas"" (""FirstSecretaryUserId"");");
        migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""IX_MeetingAtas_MeetingId"" ON ""MeetingAtas"" (""MeetingId"");");
        migrationBuilder.Sql(@"CREATE INDEX ""IX_MeetingAtas_PresidentUserId"" ON ""MeetingAtas"" (""PresidentUserId"");");
        migrationBuilder.Sql(@"CREATE INDEX ""IX_MeetingAtas_SecondSecretaryUserId"" ON ""MeetingAtas"" (""SecondSecretaryUserId"");");
    }
}
