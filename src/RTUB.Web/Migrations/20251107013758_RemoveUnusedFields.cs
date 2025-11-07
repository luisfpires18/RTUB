using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Manually rebuild Transactions table to remove Receipt fields
            // Using raw SQL to avoid EF Core's automatic PRAGMA generation within transactions
            migrationBuilder.Sql(@"
                CREATE TABLE ""Transactions_new"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Transactions"" PRIMARY KEY AUTOINCREMENT,
                    ""ActivityId"" INTEGER NULL,
                    ""Amount"" TEXT NOT NULL,
                    ""Category"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""Date"" TEXT NOT NULL,
                    ""Description"" TEXT NULL,
                    ""Type"" TEXT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""UpdatedBy"" TEXT NULL,
                    CONSTRAINT ""FK_Transactions_Activities_ActivityId"" FOREIGN KEY (""ActivityId"") REFERENCES ""Activities"" (""Id"") ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Transactions_new""
                    (""Id"", ""ActivityId"", ""Amount"", ""Category"", ""CreatedAt"", ""CreatedBy"", 
                     ""Date"", ""Description"", ""Type"", ""UpdatedAt"", ""UpdatedBy"")
                SELECT
                    ""Id"", ""ActivityId"", ""Amount"", ""Category"", ""CreatedAt"", ""CreatedBy"", 
                    ""Date"", ""Description"", ""Type"", ""UpdatedAt"", ""UpdatedBy""
                FROM ""Transactions"";
            ");

            migrationBuilder.Sql(@"DROP TABLE ""Transactions"";");
            
            migrationBuilder.Sql(@"ALTER TABLE ""Transactions_new"" RENAME TO ""Transactions"";");
            
            migrationBuilder.Sql(@"CREATE INDEX ""IX_Transactions_ActivityId"" ON ""Transactions"" (""ActivityId"");");

            // Manually rebuild Instruments table to remove PurchaseDate and PurchasePrice
            migrationBuilder.Sql(@"
                CREATE TABLE ""Instruments_new"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Instruments"" PRIMARY KEY AUTOINCREMENT,
                    ""Brand"" TEXT NULL,
                    ""Category"" TEXT NOT NULL DEFAULT '',
                    ""Condition"" INTEGER NOT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""ImageContentType"" TEXT NULL,
                    ""ImageData"" BLOB NULL,
                    ""LastMaintenanceDate"" TEXT NULL,
                    ""Location"" TEXT NULL,
                    ""MaintenanceNotes"" TEXT NULL,
                    ""Name"" TEXT NOT NULL,
                    ""SerialNumber"" TEXT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""UpdatedBy"" TEXT NULL
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Instruments_new""
                    (""Id"", ""Brand"", ""Category"", ""Condition"", ""CreatedAt"", ""CreatedBy"",
                     ""ImageContentType"", ""ImageData"", ""LastMaintenanceDate"", ""Location"",
                     ""MaintenanceNotes"", ""Name"", ""SerialNumber"", ""UpdatedAt"", ""UpdatedBy"")
                SELECT
                    ""Id"", ""Brand"", ""Category"", ""Condition"", ""CreatedAt"", ""CreatedBy"",
                    ""ImageContentType"", ""ImageData"", ""LastMaintenanceDate"", ""Location"",
                    ""MaintenanceNotes"", ""Name"", ""SerialNumber"", ""UpdatedAt"", ""UpdatedBy""
                FROM ""Instruments"";
            ");

            migrationBuilder.Sql(@"DROP TABLE ""Instruments"";");
            
            migrationBuilder.Sql(@"ALTER TABLE ""Instruments_new"" RENAME TO ""Instruments"";");

            // Manually rebuild Enrollments table to remove Attended
            migrationBuilder.Sql(@"
                CREATE TABLE ""Enrollments_new"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Enrollments"" PRIMARY KEY AUTOINCREMENT,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""EnrolledAt"" TEXT NOT NULL,
                    ""EventId"" INTEGER NOT NULL,
                    ""Instrument"" INTEGER NULL,
                    ""Notes"" TEXT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""UpdatedBy"" TEXT NULL,
                    ""UserId"" TEXT NOT NULL,
                    CONSTRAINT ""FK_Enrollments_AspNetUsers_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""AspNetUsers"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_Enrollments_Events_EventId"" FOREIGN KEY (""EventId"") REFERENCES ""Events"" (""Id"") ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Enrollments_new""
                    (""Id"", ""CreatedAt"", ""CreatedBy"", ""EnrolledAt"", ""EventId"", ""Instrument"", ""Notes"", ""UpdatedAt"", ""UpdatedBy"", ""UserId"")
                SELECT
                    ""Id"", ""CreatedAt"", ""CreatedBy"", ""EnrolledAt"", ""EventId"", ""Instrument"", ""Notes"", ""UpdatedAt"", ""UpdatedBy"", ""UserId""
                FROM ""Enrollments"";
            ");

            migrationBuilder.Sql(@"DROP TABLE ""Enrollments"";");
            
            migrationBuilder.Sql(@"ALTER TABLE ""Enrollments_new"" RENAME TO ""Enrollments"";");
            
            migrationBuilder.Sql(@"CREATE INDEX ""IX_Enrollments_EventId"" ON ""Enrollments"" (""EventId"");");
            migrationBuilder.Sql(@"CREATE INDEX ""IX_Enrollments_UserId"" ON ""Enrollments"" (""UserId"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Manually rebuild Transactions table to add Receipt fields back
            migrationBuilder.Sql(@"
                CREATE TABLE ""Transactions_old"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Transactions"" PRIMARY KEY AUTOINCREMENT,
                    ""ActivityId"" INTEGER NULL,
                    ""Amount"" TEXT NOT NULL,
                    ""Category"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""Date"" TEXT NOT NULL,
                    ""Description"" TEXT NULL,
                    ""ReceiptContentType"" TEXT NULL,
                    ""ReceiptData"" BLOB NULL,
                    ""Type"" TEXT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""UpdatedBy"" TEXT NULL,
                    CONSTRAINT ""FK_Transactions_Activities_ActivityId"" FOREIGN KEY (""ActivityId"") REFERENCES ""Activities"" (""Id"") ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Transactions_old""
                    (""Id"", ""ActivityId"", ""Amount"", ""Category"", ""CreatedAt"", ""CreatedBy"", 
                     ""Date"", ""Description"", ""Type"", ""UpdatedAt"", ""UpdatedBy"", ""ReceiptContentType"", ""ReceiptData"")
                SELECT
                    ""Id"", ""ActivityId"", ""Amount"", ""Category"", ""CreatedAt"", ""CreatedBy"", 
                    ""Date"", ""Description"", ""Type"", ""UpdatedAt"", ""UpdatedBy"", NULL, NULL
                FROM ""Transactions"";
            ");

            migrationBuilder.Sql(@"DROP TABLE ""Transactions"";");
            
            migrationBuilder.Sql(@"ALTER TABLE ""Transactions_old"" RENAME TO ""Transactions"";");
            
            migrationBuilder.Sql(@"CREATE INDEX ""IX_Transactions_ActivityId"" ON ""Transactions"" (""ActivityId"");");

            // Manually rebuild Instruments table to add PurchaseDate and PurchasePrice back
            migrationBuilder.Sql(@"
                CREATE TABLE ""Instruments_old"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Instruments"" PRIMARY KEY AUTOINCREMENT,
                    ""Brand"" TEXT NULL,
                    ""Category"" TEXT NOT NULL DEFAULT '',
                    ""Condition"" INTEGER NOT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""ImageContentType"" TEXT NULL,
                    ""ImageData"" BLOB NULL,
                    ""LastMaintenanceDate"" TEXT NULL,
                    ""Location"" TEXT NULL,
                    ""MaintenanceNotes"" TEXT NULL,
                    ""Name"" TEXT NOT NULL,
                    ""PurchaseDate"" TEXT NULL,
                    ""PurchasePrice"" TEXT NULL,
                    ""SerialNumber"" TEXT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""UpdatedBy"" TEXT NULL
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Instruments_old""
                    (""Id"", ""Brand"", ""Category"", ""Condition"", ""CreatedAt"", ""CreatedBy"",
                     ""ImageContentType"", ""ImageData"", ""LastMaintenanceDate"", ""Location"",
                     ""MaintenanceNotes"", ""Name"", ""SerialNumber"", ""UpdatedAt"", ""UpdatedBy"", ""PurchaseDate"", ""PurchasePrice"")
                SELECT
                    ""Id"", ""Brand"", ""Category"", ""Condition"", ""CreatedAt"", ""CreatedBy"",
                    ""ImageContentType"", ""ImageData"", ""LastMaintenanceDate"", ""Location"",
                    ""MaintenanceNotes"", ""Name"", ""SerialNumber"", ""UpdatedAt"", ""UpdatedBy"", NULL, NULL
                FROM ""Instruments"";
            ");

            migrationBuilder.Sql(@"DROP TABLE ""Instruments"";");
            
            migrationBuilder.Sql(@"ALTER TABLE ""Instruments_old"" RENAME TO ""Instruments"";");

            // Manually rebuild Enrollments table to add Attended back
            migrationBuilder.Sql(@"
                CREATE TABLE ""Enrollments_old"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Enrollments"" PRIMARY KEY AUTOINCREMENT,
                    ""Attended"" INTEGER NOT NULL DEFAULT 0,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""EnrolledAt"" TEXT NOT NULL,
                    ""EventId"" INTEGER NOT NULL,
                    ""Instrument"" INTEGER NULL,
                    ""Notes"" TEXT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""UpdatedBy"" TEXT NULL,
                    ""UserId"" TEXT NOT NULL,
                    CONSTRAINT ""FK_Enrollments_AspNetUsers_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""AspNetUsers"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_Enrollments_Events_EventId"" FOREIGN KEY (""EventId"") REFERENCES ""Events"" (""Id"") ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Enrollments_old""
                    (""Id"", ""CreatedAt"", ""CreatedBy"", ""EnrolledAt"", ""EventId"", ""Instrument"", ""Notes"", ""UpdatedAt"", ""UpdatedBy"", ""UserId"", ""Attended"")
                SELECT
                    ""Id"", ""CreatedAt"", ""CreatedBy"", ""EnrolledAt"", ""EventId"", ""Instrument"", ""Notes"", ""UpdatedAt"", ""UpdatedBy"", ""UserId"", 0
                FROM ""Enrollments"";
            ");

            migrationBuilder.Sql(@"DROP TABLE ""Enrollments"";");
            
            migrationBuilder.Sql(@"ALTER TABLE ""Enrollments_old"" RENAME TO ""Enrollments"";");
            
            migrationBuilder.Sql(@"CREATE INDEX ""IX_Enrollments_EventId"" ON ""Enrollments"" (""EventId"");");
            migrationBuilder.Sql(@"CREATE INDEX ""IX_Enrollments_UserId"" ON ""Enrollments"" (""UserId"");");
        }
    }
}
