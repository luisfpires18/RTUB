using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberInstrumentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberInstruments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MemberId = table.Column<string>(type: "TEXT", nullable: false),
                    InstrumentType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberInstruments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberInstruments_AspNetUsers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberInstruments_MemberId",
                table: "MemberInstruments",
                column: "MemberId");

            // Migrate existing MainInstrument data to MemberInstruments table
            // Only migrate users who have a MainInstrument value set
            migrationBuilder.Sql(@"
                INSERT INTO MemberInstruments (MemberId, InstrumentType, IsPrimary, CreatedAt, CreatedBy)
                SELECT Id, MainInstrument, 1, datetime('now'), 'System'
                FROM AspNetUsers
                WHERE MainInstrument IS NOT NULL
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Before dropping the table, we don't need to migrate data back
            // because MainInstrument field is still there for backward compatibility
            migrationBuilder.DropTable(
                name: "MemberInstruments");
        }
    }
}
