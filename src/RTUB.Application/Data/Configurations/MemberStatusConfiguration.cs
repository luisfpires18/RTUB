using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for MemberStatus entity
/// </summary>
public class MemberStatusConfiguration : IEntityTypeConfiguration<MemberStatus>
{
    public void Configure(EntityTypeBuilder<MemberStatus> builder)
    {
        builder.HasKey(ms => ms.Id);

        builder.Property(ms => ms.UserId)
            .IsRequired()
            .HasMaxLength(450); // Standard ASP.NET Identity user ID length

        builder.Property(ms => ms.IsRetired)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(ms => ms.HasAnyActivity)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(ms => ms.ProgressDescription)
            .HasMaxLength(200);

        builder.Property(ms => ms.LastUpdatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(ms => ms.User)
            .WithMany()
            .HasForeignKey(ms => ms.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(ms => ms.UserId)
            .IsUnique()
            .HasDatabaseName("IX_MemberStatus_UserId");

        builder.HasIndex(ms => ms.LastUpdatedAt)
            .HasDatabaseName("IX_MemberStatus_LastUpdatedAt");
    }
}
