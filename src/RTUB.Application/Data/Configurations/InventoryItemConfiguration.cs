using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for InventoryItem entity
/// Maps domain entity to database schema
/// </summary>
public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.UserId)
            .IsRequired()
            .HasMaxLength(450); // Standard Identity user ID length

        builder.Property(i => i.Type)
            .IsRequired();

        builder.Property(i => i.Quantity)
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        // Unique constraint: one row per user per item type
        builder.HasIndex(i => new { i.UserId, i.Type })
            .IsUnique()
            .HasDatabaseName("IX_InventoryItems_UserId_Type");

        // Index on UserId for performance when querying user's inventory
        builder.HasIndex(i => i.UserId)
            .HasDatabaseName("IX_InventoryItems_UserId");

        // Relationship
        builder.HasOne(i => i.User)
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade); // Delete inventory when user is deleted
    }
}
