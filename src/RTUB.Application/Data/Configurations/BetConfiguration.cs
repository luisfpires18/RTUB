using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Bet entity
/// Maps domain entity to database schema
/// </summary>
public class BetConfiguration : IEntityTypeConfiguration<Bet>
{
    public void Configure(EntityTypeBuilder<Bet> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.Description)
            .HasMaxLength(2000);

        builder.Property(b => b.Location)
            .HasMaxLength(200);

        builder.Property(b => b.ImageSrc)
            .HasMaxLength(500);

        builder.Property(b => b.DateTime)
            .IsRequired();

        builder.Property(b => b.BetCategory)
            .IsRequired();

        builder.Property(b => b.CancellationReason)
            .HasMaxLength(1000);

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(b => b.DateTime)
            .HasDatabaseName("IX_Bets_DateTime");

        builder.HasIndex(b => b.BetCategory)
            .HasDatabaseName("IX_Bets_BetCategory");

        builder.HasIndex(b => b.IsCancelled)
            .HasDatabaseName("IX_Bets_IsCancelled");

        // Relationships
        builder.HasMany(b => b.Options)
            .WithOne(o => o.Bet)
            .HasForeignKey(o => o.BetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.UserBets)
            .WithOne(ub => ub.Bet)
            .HasForeignKey(ub => ub.BetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
