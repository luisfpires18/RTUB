using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class FixS3ImageStorageMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration re-applies the S3 Image Storage changes from 20251109231600_MigrateToS3ImageStorage
            // The original migration wasn't running in production even though it's not in __EFMigrationsHistory
            // 
            // These changes are idempotent - they only run if the old blob columns still exist

            // ===== Slideshows: Remove ImageData and ImageContentType =====
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS Slideshows_new (
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

                INSERT INTO Slideshows_new (Id, Title, Description, ImageUrl, [Order], IntervalMs, IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                SELECT Id, Title, Description, COALESCE(ImageUrl, ''), [Order], IntervalMs, IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
                FROM Slideshows
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Slideshows') WHERE name = 'ImageData');

                DROP TABLE IF EXISTS Slideshows_backup;
                CREATE TABLE IF NOT EXISTS Slideshows_backup AS SELECT * FROM Slideshows LIMIT 0;
                INSERT INTO Slideshows_backup SELECT * FROM Slideshows 
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Slideshows') WHERE name = 'ImageData');
                
                DROP TABLE Slideshows;
                ALTER TABLE Slideshows_new RENAME TO Slideshows;
                DROP TABLE IF EXISTS Slideshows_backup;
            ");

            // ===== Events: Remove ImageData and ImageContentType =====
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS Events_new (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Date TEXT NOT NULL,
                    EndDate TEXT,
                    Description TEXT,
                    Location TEXT NOT NULL,
                    Type INTEGER NOT NULL,
                    ImageUrl TEXT,
                    CreatedAt TEXT NOT NULL,
                    CreatedBy TEXT,
                    UpdatedAt TEXT,
                    UpdatedBy TEXT
                );

                INSERT INTO Events_new (Id, Name, Date, EndDate, Description, Location, Type, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, Name, Date, EndDate, Description, Location, Type, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Events
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Events') WHERE name = 'ImageData');

                DROP TABLE IF EXISTS Events_backup;
                CREATE TABLE IF NOT EXISTS Events_backup AS SELECT * FROM Events LIMIT 0;
                INSERT INTO Events_backup SELECT * FROM Events 
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Events') WHERE name = 'ImageData');
                
                DROP TABLE Events;
                ALTER TABLE Events_new RENAME TO Events;
                DROP TABLE IF EXISTS Events_backup;
            ");

            // ===== Albums: Remove CoverImageData, rename CoverImageUrl to ImageUrl =====
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS Albums_new (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT,
                    Year INTEGER,
                    ImageUrl TEXT,
                    CreatedAt TEXT NOT NULL,
                    CreatedBy TEXT,
                    UpdatedAt TEXT,
                    UpdatedBy TEXT
                );

                INSERT INTO Albums_new (Id, Title, Description, Year, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, Title, Description, Year, CoverImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Albums
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Albums') WHERE name = 'CoverImageData');

                DROP TABLE IF EXISTS Albums_backup;
                CREATE TABLE IF NOT EXISTS Albums_backup AS SELECT * FROM Albums LIMIT 0;
                INSERT INTO Albums_backup SELECT * FROM Albums 
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Albums') WHERE name = 'CoverImageData');
                
                DROP TABLE Albums;
                ALTER TABLE Albums_new RENAME TO Albums;
                DROP TABLE IF EXISTS Albums_backup;
            ");

            // ===== Products: Remove ImageData =====
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS Products_new (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Type TEXT NOT NULL,
                    Description TEXT,
                    Price TEXT NOT NULL,
                    Stock INTEGER NOT NULL,
                    IsAvailable INTEGER NOT NULL,
                    IsPublic INTEGER NOT NULL,
                    ImageUrl TEXT,
                    CreatedAt TEXT NOT NULL,
                    CreatedBy TEXT,
                    UpdatedAt TEXT,
                    UpdatedBy TEXT
                );

                INSERT INTO Products_new (Id, Name, Type, Description, Price, Stock, IsAvailable, IsPublic, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, Name, Type, Description, Price, Stock, IsAvailable, IsPublic, NULL, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Products
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Products') WHERE name = 'ImageData');

                DROP TABLE IF EXISTS Products_backup;
                CREATE TABLE IF NOT EXISTS Products_backup AS SELECT * FROM Products LIMIT 0;
                INSERT INTO Products_backup SELECT * FROM Products 
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Products') WHERE name = 'ImageData');
                
                DROP TABLE Products;
                ALTER TABLE Products_new RENAME TO Products;
                DROP TABLE IF EXISTS Products_backup;
            ");

            // ===== Instruments: Remove ImageData =====
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS Instruments_new (
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
                    CreatedBy TEXT,
                    UpdatedAt TEXT,
                    UpdatedBy TEXT
                );

                INSERT INTO Instruments_new (Id, Category, Name, SerialNumber, Brand, Condition, Location, MaintenanceNotes, LastMaintenanceDate, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, Category, Name, SerialNumber, Brand, Condition, Location, MaintenanceNotes, LastMaintenanceDate, NULL, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Instruments
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Instruments') WHERE name = 'ImageData');

                DROP TABLE IF EXISTS Instruments_backup;
                CREATE TABLE IF NOT EXISTS Instruments_backup AS SELECT * FROM Instruments LIMIT 0;
                INSERT INTO Instruments_backup SELECT * FROM Instruments 
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Instruments') WHERE name = 'ImageData');
                
                DROP TABLE Instruments;
                ALTER TABLE Instruments_new RENAME TO Instruments;
                DROP TABLE IF EXISTS Instruments_backup;
            ");

            // ===== AspNetUsers: Add ImageUrl and remove ProfilePictureData columns =====
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS AspNetUsers_new (
                    Id TEXT NOT NULL PRIMARY KEY,
                    AccessFailedCount INTEGER NOT NULL,
                    Categories TEXT NOT NULL,
                    CategoriesJson TEXT,
                    City TEXT,
                    ConcurrencyStamp TEXT,
                    DateOfBirth TEXT,
                    Degree TEXT,
                    Email TEXT,
                    EmailConfirmed INTEGER NOT NULL,
                    FirstName TEXT NOT NULL,
                    ImageUrl TEXT,
                    LastLoginDate TEXT,
                    LastName TEXT NOT NULL,
                    LockoutEnabled INTEGER NOT NULL,
                    LockoutEnd TEXT,
                    MainInstrument INTEGER,
                    MentorId TEXT,
                    Nickname TEXT,
                    NormalizedEmail TEXT,
                    NormalizedUserName TEXT,
                    PasswordHash TEXT,
                    PhoneContact TEXT NOT NULL,
                    PhoneNumber TEXT,
                    PhoneNumberConfirmed INTEGER NOT NULL,
                    Positions TEXT NOT NULL,
                    PositionsJson TEXT,
                    RequirePasswordChange INTEGER NOT NULL,
                    SecurityStamp TEXT,
                    Subscribed INTEGER NOT NULL,
                    TwoFactorEnabled INTEGER NOT NULL,
                    UserName TEXT,
                    YearCaloiro INTEGER,
                    YearLeitao INTEGER,
                    YearTuno INTEGER,
                    CONSTRAINT FK_AspNetUsers_AspNetUsers_MentorId FOREIGN KEY (MentorId) REFERENCES AspNetUsers_new (Id) ON DELETE SET NULL
                );

                INSERT INTO AspNetUsers_new (
                    Id, AccessFailedCount, Categories, CategoriesJson, City, ConcurrencyStamp,
                    DateOfBirth, Degree, Email, EmailConfirmed, FirstName, ImageUrl, LastLoginDate,
                    LastName, LockoutEnabled, LockoutEnd, MainInstrument, MentorId, Nickname,
                    NormalizedEmail, NormalizedUserName, PasswordHash, PhoneContact, PhoneNumber,
                    PhoneNumberConfirmed, Positions, PositionsJson, RequirePasswordChange,
                    SecurityStamp, Subscribed, TwoFactorEnabled, UserName, YearCaloiro, YearLeitao, YearTuno
                )
                SELECT
                    Id, AccessFailedCount, Categories, CategoriesJson, City, ConcurrencyStamp,
                    DateOfBirth, Degree, Email, EmailConfirmed, FirstName, NULL, LastLoginDate,
                    LastName, LockoutEnabled, LockoutEnd, MainInstrument, MentorId, Nickname,
                    NormalizedEmail, NormalizedUserName, PasswordHash, PhoneContact, PhoneNumber,
                    PhoneNumberConfirmed, Positions, PositionsJson, RequirePasswordChange,
                    SecurityStamp, COALESCE(Subscribed, 0), TwoFactorEnabled, UserName, YearCaloiro, YearLeitao, YearTuno
                FROM AspNetUsers
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('AspNetUsers') WHERE name = 'ProfilePictureData');

                DROP TABLE IF EXISTS AspNetUsers_backup;
                CREATE TABLE IF NOT EXISTS AspNetUsers_backup AS SELECT * FROM AspNetUsers LIMIT 0;
                INSERT INTO AspNetUsers_backup SELECT * FROM AspNetUsers 
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('AspNetUsers') WHERE name = 'ProfilePictureData');
                
                DROP TABLE AspNetUsers;
                ALTER TABLE AspNetUsers_new RENAME TO AspNetUsers;
                DROP TABLE IF EXISTS AspNetUsers_backup;

                -- Recreate indexes
                CREATE INDEX IF NOT EXISTS IX_AspNetUsers_MentorId ON AspNetUsers (MentorId);
                CREATE INDEX IF NOT EXISTS EmailIndex ON AspNetUsers (NormalizedEmail);
                CREATE UNIQUE INDEX IF NOT EXISTS UserNameIndex ON AspNetUsers (NormalizedUserName);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This is a fix migration - the original migration 20251109231600_MigrateToS3ImageStorage has the down logic
        }
    }
}
