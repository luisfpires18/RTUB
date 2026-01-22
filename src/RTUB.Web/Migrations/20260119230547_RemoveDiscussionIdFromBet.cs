using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class RemoveDiscussionIdFromBet : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // This migration was intended to remove the DiscussionId column from Bets table
        // However, the column may not exist in all databases (it was removed from the model
        // but never properly migrated in all environments)
        // 
        // This is now a no-op migration to ensure compatibility across all database states.
        // The delete cascade issue is handled by ExecuteDeleteAsync in BetService.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No-op - nothing to reverse
    }
}
