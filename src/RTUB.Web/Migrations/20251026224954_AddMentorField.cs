using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite requires manual table rebuild for adding self-referential foreign key
            // Using raw SQL to avoid EF Core's automatic PRAGMA generation within transactions
            
            // Step 1: Create new table with MentorId column and foreign key
            migrationBuilder.Sql(@"
                CREATE TABLE ""AspNetUsers_new"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_AspNetUsers"" PRIMARY KEY,
                    ""AccessFailedCount"" INTEGER NOT NULL,
                    ""Categories"" TEXT NULL,
                    ""CategoriesJson"" TEXT NULL,
                    ""ConcurrencyStamp"" TEXT NULL,
                    ""DateOfBirth"" TEXT NULL,
                    ""Degree"" TEXT NULL,
                    ""Email"" TEXT NULL,
                    ""EmailConfirmed"" INTEGER NOT NULL,
                    ""FirstName"" TEXT NULL,
                    ""IsActive"" INTEGER NOT NULL,
                    ""LastName"" TEXT NULL,
                    ""LockoutEnabled"" INTEGER NOT NULL,
                    ""LockoutEnd"" TEXT NULL,
                    ""MainInstrument"" INTEGER NULL,
                    ""Nickname"" TEXT NULL,
                    ""NormalizedEmail"" TEXT NULL,
                    ""NormalizedUserName"" TEXT NULL,
                    ""PasswordHash"" TEXT NULL,
                    ""PhoneContact"" TEXT NULL,
                    ""PhoneNumber"" TEXT NULL,
                    ""PhoneNumberConfirmed"" INTEGER NOT NULL,
                    ""Positions"" TEXT NULL,
                    ""PositionsJson"" TEXT NULL,
                    ""ProfilePictureContentType"" TEXT NULL,
                    ""ProfilePictureData"" BLOB NULL,
                    ""SecurityStamp"" TEXT NULL,
                    ""TwoFactorEnabled"" INTEGER NOT NULL,
                    ""UserName"" TEXT NULL,
                    ""MentorId"" TEXT NULL,
                    CONSTRAINT ""FK_AspNetUsers_AspNetUsers_MentorId"" FOREIGN KEY (""MentorId"") REFERENCES ""AspNetUsers"" (""Id"") ON DELETE SET NULL
                );
            ");

            // Step 2: Copy data from old table to new table
            migrationBuilder.Sql(@"
                INSERT INTO ""AspNetUsers_new""
                    (""Id"", ""AccessFailedCount"", ""Categories"", ""CategoriesJson"", ""ConcurrencyStamp"",
                     ""DateOfBirth"", ""Degree"", ""Email"", ""EmailConfirmed"", ""FirstName"", ""IsActive"",
                     ""LastName"", ""LockoutEnabled"", ""LockoutEnd"", ""MainInstrument"", ""Nickname"",
                     ""NormalizedEmail"", ""NormalizedUserName"", ""PasswordHash"", ""PhoneContact"",
                     ""PhoneNumber"", ""PhoneNumberConfirmed"", ""Positions"", ""PositionsJson"",
                     ""ProfilePictureContentType"", ""ProfilePictureData"", ""SecurityStamp"",
                     ""TwoFactorEnabled"", ""UserName"", ""MentorId"")
                SELECT
                    ""Id"", ""AccessFailedCount"", ""Categories"", ""CategoriesJson"", ""ConcurrencyStamp"",
                    ""DateOfBirth"", ""Degree"", ""Email"", ""EmailConfirmed"", ""FirstName"", ""IsActive"",
                    ""LastName"", ""LockoutEnabled"", ""LockoutEnd"", ""MainInstrument"", ""Nickname"",
                    ""NormalizedEmail"", ""NormalizedUserName"", ""PasswordHash"", ""PhoneContact"",
                    ""PhoneNumber"", ""PhoneNumberConfirmed"", ""Positions"", ""PositionsJson"",
                    ""ProfilePictureContentType"", ""ProfilePictureData"", ""SecurityStamp"",
                    ""TwoFactorEnabled"", ""UserName"", NULL
                FROM ""AspNetUsers"";
            ");

            // Step 3: Drop old table
            migrationBuilder.Sql(@"DROP TABLE ""AspNetUsers"";");

            // Step 4: Rename new table
            migrationBuilder.Sql(@"ALTER TABLE ""AspNetUsers_new"" RENAME TO ""AspNetUsers"";");

            // Step 5: Recreate indexes
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""UserNameIndex"" ON ""AspNetUsers"" (""NormalizedUserName"");");
            migrationBuilder.Sql(@"CREATE INDEX ""EmailIndex"" ON ""AspNetUsers"" (""NormalizedEmail"");");
            migrationBuilder.Sql(@"CREATE INDEX ""IX_AspNetUsers_MentorId"" ON ""AspNetUsers"" (""MentorId"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite requires manual table rebuild for removing self-referential foreign key
            // Using raw SQL to avoid EF Core's automatic PRAGMA generation within transactions
            
            // Step 1: Create new table without MentorId column and foreign key
            migrationBuilder.Sql(@"
                CREATE TABLE ""AspNetUsers_old"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_AspNetUsers"" PRIMARY KEY,
                    ""AccessFailedCount"" INTEGER NOT NULL,
                    ""Categories"" TEXT NULL,
                    ""CategoriesJson"" TEXT NULL,
                    ""ConcurrencyStamp"" TEXT NULL,
                    ""DateOfBirth"" TEXT NULL,
                    ""Degree"" TEXT NULL,
                    ""Email"" TEXT NULL,
                    ""EmailConfirmed"" INTEGER NOT NULL,
                    ""FirstName"" TEXT NULL,
                    ""IsActive"" INTEGER NOT NULL,
                    ""LastName"" TEXT NULL,
                    ""LockoutEnabled"" INTEGER NOT NULL,
                    ""LockoutEnd"" TEXT NULL,
                    ""MainInstrument"" INTEGER NULL,
                    ""Nickname"" TEXT NULL,
                    ""NormalizedEmail"" TEXT NULL,
                    ""NormalizedUserName"" TEXT NULL,
                    ""PasswordHash"" TEXT NULL,
                    ""PhoneContact"" TEXT NULL,
                    ""PhoneNumber"" TEXT NULL,
                    ""PhoneNumberConfirmed"" INTEGER NOT NULL,
                    ""Positions"" TEXT NULL,
                    ""PositionsJson"" TEXT NULL,
                    ""ProfilePictureContentType"" TEXT NULL,
                    ""ProfilePictureData"" BLOB NULL,
                    ""SecurityStamp"" TEXT NULL,
                    ""TwoFactorEnabled"" INTEGER NOT NULL,
                    ""UserName"" TEXT NULL
                );
            ");

            // Step 2: Copy data from current table to old table (excluding MentorId)
            migrationBuilder.Sql(@"
                INSERT INTO ""AspNetUsers_old""
                    (""Id"", ""AccessFailedCount"", ""Categories"", ""CategoriesJson"", ""ConcurrencyStamp"",
                     ""DateOfBirth"", ""Degree"", ""Email"", ""EmailConfirmed"", ""FirstName"", ""IsActive"",
                     ""LastName"", ""LockoutEnabled"", ""LockoutEnd"", ""MainInstrument"", ""Nickname"",
                     ""NormalizedEmail"", ""NormalizedUserName"", ""PasswordHash"", ""PhoneContact"",
                     ""PhoneNumber"", ""PhoneNumberConfirmed"", ""Positions"", ""PositionsJson"",
                     ""ProfilePictureContentType"", ""ProfilePictureData"", ""SecurityStamp"",
                     ""TwoFactorEnabled"", ""UserName"")
                SELECT
                    ""Id"", ""AccessFailedCount"", ""Categories"", ""CategoriesJson"", ""ConcurrencyStamp"",
                    ""DateOfBirth"", ""Degree"", ""Email"", ""EmailConfirmed"", ""FirstName"", ""IsActive"",
                    ""LastName"", ""LockoutEnabled"", ""LockoutEnd"", ""MainInstrument"", ""Nickname"",
                    ""NormalizedEmail"", ""NormalizedUserName"", ""PasswordHash"", ""PhoneContact"",
                    ""PhoneNumber"", ""PhoneNumberConfirmed"", ""Positions"", ""PositionsJson"",
                    ""ProfilePictureContentType"", ""ProfilePictureData"", ""SecurityStamp"",
                    ""TwoFactorEnabled"", ""UserName""
                FROM ""AspNetUsers"";
            ");

            // Step 3: Drop current table
            migrationBuilder.Sql(@"DROP TABLE ""AspNetUsers"";");

            // Step 4: Rename old table
            migrationBuilder.Sql(@"ALTER TABLE ""AspNetUsers_old"" RENAME TO ""AspNetUsers"";");

            // Step 5: Recreate indexes (without MentorId index)
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""UserNameIndex"" ON ""AspNetUsers"" (""NormalizedUserName"");");
            migrationBuilder.Sql(@"CREATE INDEX ""EmailIndex"" ON ""AspNetUsers"" (""NormalizedEmail"");");
        }
    }
}
