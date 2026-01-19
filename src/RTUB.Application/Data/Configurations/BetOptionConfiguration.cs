using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for BetOption entity
/// Maps domain entity to database schema
/// </summary>
public class BetOptionConfiguration : IEntityTypeConfiguration<BetOption>
{
    public void Configure(EntityTypeBuilder<BetOption> builder)
    {
        builder.HasKey(bo => bo.Id);

        builder.Property(bo => bo.BetId)
            .IsRequired();

        builder.Property(bo => bo.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(bo => bo.Odds)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(bo => bo.CreatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(bo => bo.BetId)
            .HasDatabaseName("IX_BetOptions_BetId");

        // Relationships
        builder.HasOne(bo => bo.Bet)
            .WithMany(b => b.Options)
            .HasForeignKey(bo => bo.BetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bo => bo.MemberA)
            .WithMany()
            .HasForeignKey(bo => bo.MemberAId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(bo => bo.MemberB)
            .WithMany()
            .HasForeignKey(bo => bo.MemberBId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
