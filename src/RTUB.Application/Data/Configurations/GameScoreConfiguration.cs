using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for GameScore entity
/// Adds indexes for common query patterns to improve leaderboard performance
/// </summary>
public class GameScoreConfiguration : IEntityTypeConfiguration<GameScore>
{
    public void Configure(EntityTypeBuilder<GameScore> builder)
    {
        // Composite index for leaderboard queries (game-specific scores sorted by points/level)
        // Covers the main leaderboard query: WHERE GameKey = X ORDER BY Points DESC, MaxLevel DESC
        builder.HasIndex(e => new { e.GameKey, e.Points, e.MaxLevel })
            .HasDatabaseName("IX_GameScores_GameKey_Points_MaxLevel");

        // Unique index for user-specific score - ensures only one score per user per game
        builder.HasIndex(e => new { e.UserId, e.GameKey })
            .IsUnique()
            .HasDatabaseName("IX_GameScores_UserId_GameKey");
    }
}
