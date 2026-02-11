using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for ForgedWeapon entity.
/// Adds indexes for common query patterns (lookup by user, equipped weapons).
/// </summary>
public class ForgedWeaponConfiguration : IEntityTypeConfiguration<ForgedWeapon>
{
    public void Configure(EntityTypeBuilder<ForgedWeapon> builder)
    {
        // Index on UserId for all "get weapons for user" queries
        builder.HasIndex(w => w.UserId)
            .HasDatabaseName("IX_ForgedWeapons_UserId");

        // Composite index for "get equipped weapons for user" queries
        builder.HasIndex(w => new { w.UserId, w.IsEquipped })
            .HasDatabaseName("IX_ForgedWeapons_UserId_IsEquipped");
    }
}
