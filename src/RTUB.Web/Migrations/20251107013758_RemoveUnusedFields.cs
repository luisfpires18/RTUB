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
            // SQLite PRAGMA commands cannot be executed within a transaction
            // DropColumn operations on tables with foreign keys require table rebuild which uses PRAGMA
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            migrationBuilder.DropColumn(
                name: "ReceiptContentType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ReceiptData",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "Attended",
                table: "Enrollments");

            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite PRAGMA commands cannot be executed within a transaction
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptContentType",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ReceiptData",
                table: "Transactions",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchaseDate",
                table: "Instruments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePrice",
                table: "Instruments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Attended",
                table: "Enrollments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
        }
    }
}
