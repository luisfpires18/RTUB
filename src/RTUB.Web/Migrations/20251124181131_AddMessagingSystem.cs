using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddMessagingSystem : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Conversations",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Participants = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                LastMessageAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastMessageId = table.Column<int>(type: "INTEGER", nullable: true),
                Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                IsSystemConversation = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                IsArchived = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Conversations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Messages",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ConversationId = table.Column<int>(type: "INTEGER", nullable: false),
                SenderId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                Body = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                IsSystem = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                ReadBy = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false, defaultValue: ""),
                Link = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Messages", x => x.Id);
                table.ForeignKey(
                    name: "FK_Messages_AspNetUsers_SenderId",
                    column: x => x.SenderId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_Messages_Conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "Conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Conversation_LastMessageAt_IsArchived",
            table: "Conversations",
            columns: new[] { "LastMessageAt", "IsArchived" });

        migrationBuilder.CreateIndex(
            name: "IX_Conversation_Participants",
            table: "Conversations",
            column: "Participants");

        migrationBuilder.CreateIndex(
            name: "IX_Message_ConversationId_CreatedAt",
            table: "Messages",
            columns: new[] { "ConversationId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Message_SenderId",
            table: "Messages",
            column: "SenderId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Messages");

        migrationBuilder.DropTable(
            name: "Conversations");
    }
}
