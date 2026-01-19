using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for UserBet entity
/// Maps domain entity to database schema
/// </summary>
public class UserBetConfiguration : IEntityTypeConfiguration<UserBet>
{
    public void Configure(EntityTypeBuilder<UserBet> builder)
    {
        builder.HasKey(ub => ub.Id);

        builder.Property(ub => ub.UserId)
            .IsRequired();

        builder.Property(ub => ub.BetId)
            .IsRequired();

        builder.Property(ub => ub.BetOptionId)
            .IsRequired();

        builder.Property(ub => ub.FidelisAmount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(ub => ub.FidelisWinnings)
            .HasPrecision(18, 2);

        builder.Property(ub => ub.CreatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(ub => ub.UserId)
            .HasDatabaseName("IX_UserBets_UserId");

        builder.HasIndex(ub => ub.BetId)
            .HasDatabaseName("IX_UserBets_BetId");

        builder.HasIndex(ub => ub.BetOptionId)
            .HasDatabaseName("IX_UserBets_BetOptionId");

        // Composite index for user + bet to quickly check if user already bet
        builder.HasIndex(ub => new { ub.UserId, ub.BetId })
            .HasDatabaseName("IX_UserBets_UserId_BetId");

        // Relationships
        builder.HasOne(ub => ub.User)
            .WithMany()
            .HasForeignKey(ub => ub.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ub => ub.Bet)
            .WithMany(b => b.UserBets)
            .HasForeignKey(ub => ub.BetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ub => ub.BetOption)
            .WithMany()
            .HasForeignKey(ub => ub.BetOptionId)
            .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete conflicts
    }
}
