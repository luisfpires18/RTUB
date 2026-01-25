using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class MigratePhoneContactToPhoneNumber : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Copy data from PhoneContact to PhoneNumber only if PhoneNumber is null or empty
        migrationBuilder.Sql(
            @"UPDATE AspNetUsers 
                  SET PhoneNumber = PhoneContact 
                  WHERE (PhoneNumber IS NULL OR PhoneNumber = '') 
                    AND PhoneContact IS NOT NULL 
                    AND PhoneContact != ''");

        // Drop the PhoneContact column
        migrationBuilder.DropColumn(
            name: "PhoneContact",
            table: "AspNetUsers");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Re-add PhoneContact column as nullable
        migrationBuilder.AddColumn<string>(
            name: "PhoneContact",
            table: "AspNetUsers",
            type: "TEXT",
            maxLength: 80,
            nullable: true);

        // Copy data back from PhoneNumber to PhoneContact
        migrationBuilder.Sql(
            @"UPDATE AspNetUsers 
                  SET PhoneContact = PhoneNumber 
                  WHERE PhoneNumber IS NOT NULL AND PhoneNumber != ''");
    }
}
