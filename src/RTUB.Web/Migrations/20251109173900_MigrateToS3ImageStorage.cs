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
            // ===== STEP 1: Remove ImageData blob columns using table recreation =====
            // This approach works smoothly with SQLite and avoids PRAGMA foreign_keys issues
            
            // --- Slideshows: Remove ImageData and ImageContentType ---
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

            migrationBuilder.Sql(@"
                INSERT INTO Slideshows_new (Id, Title, Description, ImageUrl, [Order], IntervalMs, IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                SELECT Id, Title, Description, ImageUrl, [Order], IntervalMs, IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
                FROM Slideshows;
            ");

            migrationBuilder.Sql("DROP TABLE Slideshows;");
            migrationBuilder.Sql("ALTER TABLE Slideshows_new RENAME TO Slideshows;");

            // --- Events: Remove ImageData and ImageContentType ---
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
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO Events_new (Id, Name, Date, EndDate, Description, Location, Type, ImageUrl, CreatedAt, UpdatedAt)
                SELECT Id, Name, Date, EndDate, Description, Location, Type, ImageUrl, CreatedAt, UpdatedAt
                FROM Events;
            ");

            migrationBuilder.Sql("DROP TABLE Events;");
            migrationBuilder.Sql("ALTER TABLE Events_new RENAME TO Events;");

            // --- Albums: Remove CoverImageData and CoverImageContentType, rename CoverImageUrl to ImageUrl ---
            migrationBuilder.Sql(@"
                CREATE TABLE Albums_new (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT,
                    Year INTEGER,
                    ImageUrl TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO Albums_new (Id, Title, Description, Year, ImageUrl, CreatedAt, UpdatedAt)
                SELECT Id, Title, Description, Year, CoverImageUrl, CreatedAt, UpdatedAt
                FROM Albums;
            ");

            migrationBuilder.Sql("DROP TABLE Albums;");
            migrationBuilder.Sql("ALTER TABLE Albums_new RENAME TO Albums;");

            // --- Products: Remove ImageData, rename ImageContentType to ImageUrl ---
            migrationBuilder.Sql(@"
                CREATE TABLE Products_new (
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
                    UpdatedAt TEXT
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO Products_new (Id, Name, Type, Description, Price, Stock, IsAvailable, IsPublic, ImageUrl, CreatedAt, UpdatedAt)
                SELECT Id, Name, Type, Description, Price, Stock, IsAvailable, IsPublic, NULL, CreatedAt, UpdatedAt
                FROM Products;
            ");

            migrationBuilder.Sql("DROP TABLE Products;");
            migrationBuilder.Sql("ALTER TABLE Products_new RENAME TO Products;");

            // --- Instruments: Remove ImageData, rename ImageContentType to ImageUrl ---
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
                    UpdatedAt TEXT
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO Instruments_new (Id, Category, Name, SerialNumber, Brand, Condition, Location, MaintenanceNotes, LastMaintenanceDate, ImageUrl, CreatedAt, UpdatedAt)
                SELECT Id, Category, Name, SerialNumber, Brand, Condition, Location, MaintenanceNotes, LastMaintenanceDate, NULL, CreatedAt, UpdatedAt
                FROM Instruments;
            ");

            migrationBuilder.Sql("DROP TABLE Instruments;");
            migrationBuilder.Sql("ALTER TABLE Instruments_new RENAME TO Instruments;");

            // ===== STEP 2: Add ImageUrl and remove ProfilePictureData blob columns from AspNetUsers =====
            // SQLite requires table recreation to properly drop columns
            // First, create the new table structure with ImageUrl and without blob columns
            migrationBuilder.Sql(@"
                CREATE TABLE AspNetUsers_new (
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
                    CONSTRAINT FK_AspNetUsers_AspNetUsers_MentorId FOREIGN KEY (MentorId) REFERENCES AspNetUsers (Id) ON DELETE SET NULL
                );
            ");

            // Copy data from old table to new table (excluding ProfilePictureData and ProfilePictureContentType)
            migrationBuilder.Sql(@"
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
                    SecurityStamp, Subscribed, TwoFactorEnabled, UserName, YearCaloiro, YearLeitao, YearTuno
                FROM AspNetUsers;
            ");

            // Drop old table and rename new table
            migrationBuilder.Sql("DROP TABLE AspNetUsers;");
            migrationBuilder.Sql("ALTER TABLE AspNetUsers_new RENAME TO AspNetUsers;");

            // Recreate indexes
            migrationBuilder.Sql("CREATE INDEX IX_AspNetUsers_MentorId ON AspNetUsers (MentorId);");
            migrationBuilder.Sql("CREATE INDEX EmailIndex ON AspNetUsers (NormalizedEmail);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX UserNameIndex ON AspNetUsers (NormalizedUserName);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Note: ImageUrl column in Events already existed before this migration
            // so we don't drop it in the Down method
            
            // Restore blob columns for Slideshows
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

            // Restore blob columns for Events
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

            // Restore blob columns for Albums and rename ImageUrl back to CoverImageUrl
            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "Albums",
                newName: "CoverImageUrl");

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

            // Restore blob columns for Products
            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "Products",
                newName: "ImageContentType");

            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Products",
                type: "BLOB",
                nullable: true);

            // Restore blob columns for Instruments
            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "Instruments",
                newName: "ImageContentType");

            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Instruments",
                type: "BLOB",
                nullable: true);

            // Restore ProfilePictureData blob columns for AspNetUsers
            // SQLite requires table recreation to add columns back
            migrationBuilder.Sql(@"
                CREATE TABLE AspNetUsers_new (
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
                    ProfilePictureContentType TEXT,
                    ProfilePictureData BLOB,
                    RequirePasswordChange INTEGER NOT NULL,
                    SecurityStamp TEXT,
                    Subscribed INTEGER NOT NULL,
                    TwoFactorEnabled INTEGER NOT NULL,
                    UserName TEXT,
                    YearCaloiro INTEGER,
                    YearLeitao INTEGER,
                    YearTuno INTEGER,
                    CONSTRAINT FK_AspNetUsers_AspNetUsers_MentorId FOREIGN KEY (MentorId) REFERENCES AspNetUsers (Id) ON DELETE SET NULL
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO AspNetUsers_new (
                    Id, AccessFailedCount, Categories, CategoriesJson, City, ConcurrencyStamp,
                    DateOfBirth, Degree, Email, EmailConfirmed, FirstName, LastLoginDate,
                    LastName, LockoutEnabled, LockoutEnd, MainInstrument, MentorId, Nickname,
                    NormalizedEmail, NormalizedUserName, PasswordHash, PhoneContact, PhoneNumber,
                    PhoneNumberConfirmed, Positions, PositionsJson, ProfilePictureContentType,
                    ProfilePictureData, RequirePasswordChange, SecurityStamp, Subscribed,
                    TwoFactorEnabled, UserName, YearCaloiro, YearLeitao, YearTuno
                )
                SELECT
                    Id, AccessFailedCount, Categories, CategoriesJson, City, ConcurrencyStamp,
                    DateOfBirth, Degree, Email, EmailConfirmed, FirstName, LastLoginDate,
                    LastName, LockoutEnabled, LockoutEnd, MainInstrument, MentorId, Nickname,
                    NormalizedEmail, NormalizedUserName, PasswordHash, PhoneContact, PhoneNumber,
                    PhoneNumberConfirmed, Positions, PositionsJson, NULL,
                    NULL, RequirePasswordChange, SecurityStamp, Subscribed,
                    TwoFactorEnabled, UserName, YearCaloiro, YearLeitao, YearTuno
                FROM AspNetUsers;
            ");

            migrationBuilder.Sql("DROP TABLE AspNetUsers;");
            migrationBuilder.Sql("ALTER TABLE AspNetUsers_new RENAME TO AspNetUsers;");

            // Recreate indexes
            migrationBuilder.Sql("CREATE INDEX IX_AspNetUsers_MentorId ON AspNetUsers (MentorId);");
            migrationBuilder.Sql("CREATE INDEX EmailIndex ON AspNetUsers (NormalizedEmail);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX UserNameIndex ON AspNetUsers (NormalizedUserName);");
        }
    }
}
