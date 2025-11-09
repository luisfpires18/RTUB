using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantFinancialColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // For SQLite, we need to rebuild tables to remove columns
            // This is done manually to avoid transaction issues with PRAGMA statements
            
            // Reports table - recreate without financial columns
            migrationBuilder.Sql(@"
                CREATE TABLE Reports_New (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Year INTEGER NOT NULL,
                    Summary TEXT NULL,
                    PdfData BLOB NULL,
                    PublishedAt TEXT NULL,
                    IsPublished INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    CreatedBy TEXT NULL,
                    UpdatedAt TEXT NULL,
                    UpdatedBy TEXT NULL
                );
            ");
            
            migrationBuilder.Sql(@"
                INSERT INTO Reports_New (Id, Title, Year, Summary, PdfData, PublishedAt, IsPublished, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, Title, Year, Summary, PdfData, PublishedAt, IsPublished, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Reports;
            ");
            
            migrationBuilder.Sql("DROP TABLE Reports;");
            migrationBuilder.Sql("ALTER TABLE Reports_New RENAME TO Reports;");
            
            // Activities table - recreate without financial columns
            migrationBuilder.Sql(@"
                CREATE TABLE Activities_New (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    ReportId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    Description TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    CreatedBy TEXT NULL,
                    UpdatedAt TEXT NULL,
                    UpdatedBy TEXT NULL,
                    FOREIGN KEY (ReportId) REFERENCES Reports(Id) ON DELETE CASCADE
                );
            ");
            
            migrationBuilder.Sql(@"
                INSERT INTO Activities_New (Id, ReportId, Name, Description, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, ReportId, Name, Description, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Activities;
            ");
            
            migrationBuilder.Sql("DROP TABLE Activities;");
            migrationBuilder.Sql("ALTER TABLE Activities_New RENAME TO Activities;");
            
            // Recreate indexes if any existed
            migrationBuilder.Sql("CREATE INDEX IX_Activities_ReportId ON Activities (ReportId);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // For SQLite, we need to rebuild tables to add columns back
            
            // Reports table - recreate with financial columns
            migrationBuilder.Sql(@"
                CREATE TABLE Reports_New (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Year INTEGER NOT NULL,
                    TotalIncome TEXT NOT NULL DEFAULT '0',
                    TotalExpenses TEXT NOT NULL DEFAULT '0',
                    FinalBalance TEXT NOT NULL DEFAULT '0',
                    Summary TEXT NULL,
                    PdfData BLOB NULL,
                    PublishedAt TEXT NULL,
                    IsPublished INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    CreatedBy TEXT NULL,
                    UpdatedAt TEXT NULL,
                    UpdatedBy TEXT NULL
                );
            ");
            
            migrationBuilder.Sql(@"
                INSERT INTO Reports_New (Id, Title, Year, TotalIncome, TotalExpenses, FinalBalance, Summary, PdfData, PublishedAt, IsPublished, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, Title, Year, '0', '0', '0', Summary, PdfData, PublishedAt, IsPublished, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Reports;
            ");
            
            migrationBuilder.Sql("DROP TABLE Reports;");
            migrationBuilder.Sql("ALTER TABLE Reports_New RENAME TO Reports;");
            
            // Activities table - recreate with financial columns
            migrationBuilder.Sql(@"
                CREATE TABLE Activities_New (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    ReportId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    Description TEXT NULL,
                    TotalIncome TEXT NOT NULL DEFAULT '0',
                    TotalExpenses TEXT NOT NULL DEFAULT '0',
                    Balance TEXT NOT NULL DEFAULT '0',
                    CreatedAt TEXT NOT NULL,
                    CreatedBy TEXT NULL,
                    UpdatedAt TEXT NULL,
                    UpdatedBy TEXT NULL,
                    FOREIGN KEY (ReportId) REFERENCES Reports(Id) ON DELETE CASCADE
                );
            ");
            
            migrationBuilder.Sql(@"
                INSERT INTO Activities_New (Id, ReportId, Name, Description, TotalIncome, TotalExpenses, Balance, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, ReportId, Name, Description, '0', '0', '0', CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Activities;
            ");
            
            migrationBuilder.Sql("DROP TABLE Activities;");
            migrationBuilder.Sql("ALTER TABLE Activities_New RENAME TO Activities;");
            
            // Recreate indexes if any existed
            migrationBuilder.Sql("CREATE INDEX IX_Activities_ReportId ON Activities (ReportId);");
        }
    }
}
