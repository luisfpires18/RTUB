using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for NaipePlayCount entity
/// </summary>
public class NaipePlayCountConfiguration : IEntityTypeConfiguration<NaipePlayCount>
{
    public void Configure(EntityTypeBuilder<NaipePlayCount> builder)
    {
        builder.HasKey(npc => npc.Id);

        builder.Property(npc => npc.NaipeContentId)
            .IsRequired();

        builder.Property(npc => npc.UserId)
            .HasMaxLength(450);

        builder.Property(npc => npc.PlayedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(npc => npc.NaipeContent)
            .WithMany(nc => nc.PlayCounts)
            .HasForeignKey(npc => npc.NaipeContentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(npc => npc.User)
            .WithMany()
            .HasForeignKey(npc => npc.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Index for performance when querying play counts by content
        builder.HasIndex(npc => npc.NaipeContentId)
            .HasDatabaseName("IX_NaipePlayCount_NaipeContentId");

        // Index for querying by user
        builder.HasIndex(npc => npc.UserId)
            .HasDatabaseName("IX_NaipePlayCount_UserId");
    }
}
