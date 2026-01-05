using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberStatusTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    IsRetired = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LastRehearsalDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastEventDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastActivityDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasAnyActivity = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ProgressMonths = table.Column<int>(type: "INTEGER", nullable: true),
                    ProgressTotalMonths = table.Column<int>(type: "INTEGER", nullable: true),
                    ProgressDescription = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberStatuses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberStatus_LastUpdatedAt",
                table: "MemberStatuses",
                column: "LastUpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemberStatus_UserId",
                table: "MemberStatuses",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberStatuses");
        }
    }
}
