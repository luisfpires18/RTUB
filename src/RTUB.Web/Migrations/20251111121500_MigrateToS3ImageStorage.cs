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
            // This migration renames CoverImageUrl to ImageUrl in Albums table
            // All other tables already have ImageUrl, so this is the only change needed for production
            
            // ===== Albums: Rename CoverImageUrl to ImageUrl =====
            migrationBuilder.Sql("CREATE TABLE Albums_new (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Title TEXT NOT NULL, Description TEXT, Year INTEGER, ImageUrl TEXT, CreatedAt TEXT NOT NULL, CreatedBy TEXT, UpdatedAt TEXT, UpdatedBy TEXT);");
            
            migrationBuilder.Sql("INSERT INTO Albums_new (Id, Title, Description, Year, ImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) SELECT Id, Title, Description, Year, CoverImageUrl, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM Albums;");
            
            migrationBuilder.Sql("DROP TABLE Albums;");
            
            migrationBuilder.Sql("ALTER TABLE Albums_new RENAME TO Albums;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rename ImageUrl back to CoverImageUrl
            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "Albums",
                newName: "CoverImageUrl");
        }
    }
}
