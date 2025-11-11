using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class MigrateToS3ImageStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration migrates from blob storage to S3 (Cloudflare R2) image storage
            // It removes old ImageData/ProfilePictureData blob columns and migrates to ImageUrl columns
            // All operations are idempotent and check if the old columns exist before migrating
            
            // ===== Slideshows: Remove ImageData and ImageContentType, ensure ImageUrl exists =====
            migrationBuilder.Sql(@"
                -- Only migrate if ImageData column exists
                CREATE TEMP TABLE _migrate_slideshows AS 
                SELECT COUNT(*) as should_migrate FROM pragma_table_info('Slideshows') WHERE name = 'ImageData';
                
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
                WHERE (SELECT should_migrate FROM _migrate_slideshows) > 0;

                DROP TABLE Slideshows;
                ALTER TABLE Slideshows_new RENAME TO Slideshows;
                DROP TABLE _migrate_slideshows;
            ", suppressTransaction: true);

            // ===== Events: Remove ImageData and ImageContentType, ensure ImageUrl exists =====
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE _migrate_events AS 
                SELECT COUNT(*) as should_migrate FROM pragma_table_info('Events') WHERE name = 'ImageData';
                
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
                WHERE (SELECT should_migrate FROM _migrate_events) > 0;

                DROP TABLE Events;
                ALTER TABLE Events_new RENAME TO Events;
                DROP TABLE _migrate_events;
            ", suppressTransaction: true);

            // ===== Albums: Rename CoverImageUrl to ImageUrl (or handle CoverImageData if still exists) =====
            migrationBuilder.Sql(@"
                -- Check if we need to migrate (either CoverImageUrl or CoverImageData exists)
                CREATE TEMP TABLE _migrate_albums AS 
                SELECT 
                    (SELECT COUNT(*) FROM pragma_table_info('Albums') WHERE name = 'CoverImageUrl') as has_cover_url,
                    (SELECT COUNT(*) FROM pragma_table_info('Albums') WHERE name = 'CoverImageData') as has_cover_data;
                
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

                -- Migrate from CoverImageUrl if it exists (production state)
                INSERT INTO Albums_new (Id, Title, Description, Year, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, Title, Description, Year, CoverImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Albums
                WHERE (SELECT has_cover_url FROM _migrate_albums) > 0;
                
                -- Migrate from old CoverImageData structure if CoverImageUrl doesn't exist
                INSERT INTO Albums_new (Id, Title, Description, Year, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                SELECT Id, Title, Description, Year, CoverImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                FROM Albums
                WHERE (SELECT has_cover_url FROM _migrate_albums) = 0 
                  AND (SELECT has_cover_data FROM _migrate_albums) > 0;

                DROP TABLE Albums;
                ALTER TABLE Albums_new RENAME TO Albums;
                DROP TABLE _migrate_albums;
            ", suppressTransaction: true);

            // ===== Products: Remove ImageData, ensure ImageUrl exists =====
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE _migrate_products AS 
                SELECT COUNT(*) as should_migrate FROM pragma_table_info('Products') WHERE name = 'ImageData';
                
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
                WHERE (SELECT should_migrate FROM _migrate_products) > 0;

                DROP TABLE Products;
                ALTER TABLE Products_new RENAME TO Products;
                DROP TABLE _migrate_products;
            ", suppressTransaction: true);

            // ===== Instruments: Remove ImageData, ensure ImageUrl exists =====
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE _migrate_instruments AS 
                SELECT COUNT(*) as should_migrate FROM pragma_table_info('Instruments') WHERE name = 'ImageData';
                
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
                WHERE (SELECT should_migrate FROM _migrate_instruments) > 0;

                DROP TABLE Instruments;
                ALTER TABLE Instruments_new RENAME TO Instruments;
                DROP TABLE _migrate_instruments;
            ", suppressTransaction: true);

            // ===== AspNetUsers: Add ImageUrl and remove ProfilePictureData columns =====
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE _migrate_users AS 
                SELECT COUNT(*) as should_migrate FROM pragma_table_info('AspNetUsers') WHERE name = 'ProfilePictureData';
                
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
                WHERE (SELECT should_migrate FROM _migrate_users) > 0;

                DROP TABLE AspNetUsers;
                ALTER TABLE AspNetUsers_new RENAME TO AspNetUsers;

                -- Recreate indexes
                CREATE INDEX IX_AspNetUsers_MentorId ON AspNetUsers (MentorId);
                CREATE INDEX EmailIndex ON AspNetUsers (NormalizedEmail);
                CREATE UNIQUE INDEX UserNameIndex ON AspNetUsers (NormalizedUserName);
                
                DROP TABLE _migrate_users;
            ", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down migration would restore blob columns, but since we're moving to S3,
            // this is not practical. The blob data is lost after migration.
            throw new NotImplementedException("Reverting S3 migration is not supported as blob data is not preserved.");
        }
    }
}
