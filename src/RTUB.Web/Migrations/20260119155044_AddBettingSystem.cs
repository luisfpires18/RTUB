using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddBettingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FidelisBalance",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Bets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ImageSrc = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BetCategory = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCancelled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CancellationReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    WinningOptionId = table.Column<int>(type: "INTEGER", nullable: true),
                    DiscussionId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bets_Discussions_DiscussionId",
                        column: x => x.DiscussionId,
                        principalTable: "Discussions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BetOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Odds = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MemberAId = table.Column<string>(type: "TEXT", nullable: true),
                    MemberBId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BetOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BetOptions_AspNetUsers_MemberAId",
                        column: x => x.MemberAId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BetOptions_AspNetUsers_MemberBId",
                        column: x => x.MemberBId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BetOptions_Bets_BetId",
                        column: x => x.BetId,
                        principalTable: "Bets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserBets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    BetId = table.Column<int>(type: "INTEGER", nullable: false),
                    BetOptionId = table.Column<int>(type: "INTEGER", nullable: false),
                    FidelisAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    IsWon = table.Column<bool>(type: "INTEGER", nullable: true),
                    FidelisWinnings = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBets_BetOptions_BetOptionId",
                        column: x => x.BetOptionId,
                        principalTable: "BetOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBets_Bets_BetId",
                        column: x => x.BetId,
                        principalTable: "Bets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BetOptions_BetId",
                table: "BetOptions",
                column: "BetId");

            migrationBuilder.CreateIndex(
                name: "IX_BetOptions_MemberAId",
                table: "BetOptions",
                column: "MemberAId");

            migrationBuilder.CreateIndex(
                name: "IX_BetOptions_MemberBId",
                table: "BetOptions",
                column: "MemberBId");

            migrationBuilder.CreateIndex(
                name: "IX_Bets_BetCategory",
                table: "Bets",
                column: "BetCategory");

            migrationBuilder.CreateIndex(
                name: "IX_Bets_DateTime",
                table: "Bets",
                column: "DateTime");

            migrationBuilder.CreateIndex(
                name: "IX_Bets_DiscussionId",
                table: "Bets",
                column: "DiscussionId");

            migrationBuilder.CreateIndex(
                name: "IX_Bets_IsCancelled",
                table: "Bets",
                column: "IsCancelled");

            migrationBuilder.CreateIndex(
                name: "IX_UserBets_BetId",
                table: "UserBets",
                column: "BetId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBets_BetOptionId",
                table: "UserBets",
                column: "BetOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBets_UserId",
                table: "UserBets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBets_UserId_BetId",
                table: "UserBets",
                columns: new[] { "UserId", "BetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserBets");

            migrationBuilder.DropTable(
                name: "BetOptions");

            migrationBuilder.DropTable(
                name: "Bets");

            migrationBuilder.DropColumn(
                name: "FidelisBalance",
                table: "AspNetUsers");
        }
    }
}
