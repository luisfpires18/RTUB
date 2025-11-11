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
            // This migration handles the S3 image storage migration for all entities
            // It removes blob columns and ensures ImageUrl columns exist
            
            // ===== Cleanup: Remove any leftover tables from failed migrations =====
            migrationBuilder.Sql("DROP TABLE IF EXISTS Slideshows_new;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS Events_new;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS Albums_new;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS Products_new;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS Instruments_new;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS AspNetUsers_new;");
            
            // ===== Slideshows: Remove ImageData and ImageContentType (if they exist) =====
            migrationBuilder.Sql("CREATE TABLE Slideshows_new (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Title TEXT NOT NULL, Description TEXT NOT NULL, ImageUrl TEXT NOT NULL, [Order] INTEGER NOT NULL, IntervalMs INTEGER NOT NULL, IsActive INTEGER NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT, CreatedBy TEXT, UpdatedBy TEXT);");
            migrationBuilder.Sql("INSERT INTO Slideshows_new (Id, Title, Description, ImageUrl, [Order], IntervalMs, IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) SELECT Id, Title, Description, COALESCE(ImageUrl, ''), [Order], IntervalMs, IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy FROM Slideshows;");
            migrationBuilder.Sql("DROP TABLE Slideshows;");
            migrationBuilder.Sql("ALTER TABLE Slideshows_new RENAME TO Slideshows;");
            
            // ===== Events: Remove ImageData and ImageContentType (if they exist) =====
            migrationBuilder.Sql("CREATE TABLE Events_new (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Date TEXT NOT NULL, EndDate TEXT, Description TEXT, Location TEXT NOT NULL, Type INTEGER NOT NULL, ImageUrl TEXT, CreatedAt TEXT NOT NULL, CreatedBy TEXT, UpdatedAt TEXT, UpdatedBy TEXT);");
            migrationBuilder.Sql("INSERT INTO Events_new (Id, Name, Date, EndDate, Description, Location, Type, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) SELECT Id, Name, Date, EndDate, Description, Location, Type, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM Events;");
            migrationBuilder.Sql("DROP TABLE Events;");
            migrationBuilder.Sql("ALTER TABLE Events_new RENAME TO Events;");
            
            // ===== Albums: Rename CoverImageUrl to ImageUrl =====
            migrationBuilder.Sql("CREATE TABLE Albums_new (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Title TEXT NOT NULL, Description TEXT, Year INTEGER, ImageUrl TEXT, CreatedAt TEXT NOT NULL, CreatedBy TEXT, UpdatedAt TEXT, UpdatedBy TEXT);");
            migrationBuilder.Sql("INSERT INTO Albums_new (Id, Title, Description, Year, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) SELECT Id, Title, Description, Year, CoverImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM Albums;");
            migrationBuilder.Sql("DROP TABLE Albums;");
            migrationBuilder.Sql("ALTER TABLE Albums_new RENAME TO Albums;");
            
            // ===== Products: Remove ImageData (if exists), ensure ImageUrl exists =====
            migrationBuilder.Sql("CREATE TABLE Products_new (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Type TEXT NOT NULL, Description TEXT, Price TEXT NOT NULL, Stock INTEGER NOT NULL, IsAvailable INTEGER NOT NULL, IsPublic INTEGER NOT NULL, ImageUrl TEXT, CreatedAt TEXT NOT NULL, CreatedBy TEXT, UpdatedAt TEXT, UpdatedBy TEXT);");
            migrationBuilder.Sql("INSERT INTO Products_new (Id, Name, Type, Description, Price, Stock, IsAvailable, IsPublic, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) SELECT Id, Name, Type, Description, Price, Stock, IsAvailable, IsPublic, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM Products;");
            migrationBuilder.Sql("DROP TABLE Products;");
            migrationBuilder.Sql("ALTER TABLE Products_new RENAME TO Products;");
            
            // ===== Instruments: Remove ImageData (if exists), ensure ImageUrl exists =====
            migrationBuilder.Sql("CREATE TABLE Instruments_new (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Category TEXT NOT NULL, Name TEXT NOT NULL, SerialNumber TEXT, Brand TEXT, Condition INTEGER NOT NULL, Location TEXT, MaintenanceNotes TEXT, LastMaintenanceDate TEXT, ImageUrl TEXT, CreatedAt TEXT NOT NULL, CreatedBy TEXT, UpdatedAt TEXT, UpdatedBy TEXT);");
            migrationBuilder.Sql("INSERT INTO Instruments_new (Id, Category, Name, SerialNumber, Brand, Condition, Location, MaintenanceNotes, LastMaintenanceDate, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) SELECT Id, Category, Name, SerialNumber, Brand, Condition, Location, MaintenanceNotes, LastMaintenanceDate, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM Instruments;");
            migrationBuilder.Sql("DROP TABLE Instruments;");
            migrationBuilder.Sql("ALTER TABLE Instruments_new RENAME TO Instruments;");
            
            // ===== AspNetUsers: Remove ProfilePictureData (if exists), ensure ImageUrl exists =====
            migrationBuilder.Sql("CREATE TABLE AspNetUsers_new (Id TEXT NOT NULL PRIMARY KEY, AccessFailedCount INTEGER NOT NULL, Categories TEXT NOT NULL, CategoriesJson TEXT, City TEXT, ConcurrencyStamp TEXT, DateOfBirth TEXT, Degree TEXT, Email TEXT, EmailConfirmed INTEGER NOT NULL, FirstName TEXT NOT NULL, ImageUrl TEXT, LastLoginDate TEXT, LastName TEXT NOT NULL, LockoutEnabled INTEGER NOT NULL, LockoutEnd TEXT, MainInstrument INTEGER, MentorId TEXT, Nickname TEXT, NormalizedEmail TEXT, NormalizedUserName TEXT, PasswordHash TEXT, PhoneContact TEXT NOT NULL, PhoneNumber TEXT, PhoneNumberConfirmed INTEGER NOT NULL, Positions TEXT NOT NULL, PositionsJson TEXT, RequirePasswordChange INTEGER NOT NULL, SecurityStamp TEXT, Subscribed INTEGER NOT NULL, TwoFactorEnabled INTEGER NOT NULL, UserName TEXT, YearCaloiro INTEGER, YearLeitao INTEGER, YearTuno INTEGER, CONSTRAINT FK_AspNetUsers_AspNetUsers_MentorId FOREIGN KEY (MentorId) REFERENCES AspNetUsers_new (Id) ON DELETE SET NULL);");
            migrationBuilder.Sql("INSERT INTO AspNetUsers_new (Id, AccessFailedCount, Categories, CategoriesJson, City, ConcurrencyStamp, DateOfBirth, Degree, Email, EmailConfirmed, FirstName, ImageUrl, LastLoginDate, LastName, LockoutEnabled, LockoutEnd, MainInstrument, MentorId, Nickname, NormalizedEmail, NormalizedUserName, PasswordHash, PhoneContact, PhoneNumber, PhoneNumberConfirmed, Positions, PositionsJson, RequirePasswordChange, SecurityStamp, Subscribed, TwoFactorEnabled, UserName, YearCaloiro, YearLeitao, YearTuno) SELECT Id, AccessFailedCount, Categories, CategoriesJson, City, ConcurrencyStamp, DateOfBirth, Degree, Email, EmailConfirmed, FirstName, ImageUrl, LastLoginDate, LastName, LockoutEnabled, LockoutEnd, MainInstrument, MentorId, Nickname, NormalizedEmail, NormalizedUserName, PasswordHash, PhoneContact, PhoneNumber, PhoneNumberConfirmed, Positions, PositionsJson, RequirePasswordChange, SecurityStamp, Subscribed, TwoFactorEnabled, UserName, YearCaloiro, YearLeitao, YearTuno FROM AspNetUsers;");
            migrationBuilder.Sql("DROP TABLE AspNetUsers;");
            migrationBuilder.Sql("ALTER TABLE AspNetUsers_new RENAME TO AspNetUsers;");
            migrationBuilder.Sql("CREATE INDEX IX_AspNetUsers_MentorId ON AspNetUsers (MentorId);");
            migrationBuilder.Sql("CREATE INDEX EmailIndex ON AspNetUsers (NormalizedEmail);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX UserNameIndex ON AspNetUsers (NormalizedUserName);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down migration not supported - blob data would be lost
            throw new NotSupportedException("Reverting S3 migration is not supported as blob data is not preserved.");
        }
    }
}
