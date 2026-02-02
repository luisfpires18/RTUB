using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for StageEnemy entity
/// Maps domain entity to database schema
/// </summary>
public class StageEnemyConfiguration : IEntityTypeConfiguration<StageEnemy>
{
    public void Configure(EntityTypeBuilder<StageEnemy> builder)
    {
        builder.HasKey(se => se.Id);

        builder.Property(se => se.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(se => se.Type)
            .IsRequired();

        builder.Property(se => se.Region)
            .IsRequired();

        builder.Property(se => se.BaseHP)
            .IsRequired();

        builder.Property(se => se.BasePower)
            .IsRequired();

        builder.Property(se => se.BaseSpeed)
            .IsRequired();

        builder.Property(se => se.BaseCriticalChance)
            .IsRequired();

        builder.Property(se => se.SpritePath)
            .HasMaxLength(500);

        builder.Property(se => se.BaseFidelisDrop)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(se => se.BeerDropChance)
            .IsRequired();

        builder.Property(se => se.ShotDropChance)
            .IsRequired();

        builder.Property(se => se.CreatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(se => se.Type)
            .HasDatabaseName("IX_StageEnemies_Type");

        builder.HasIndex(se => se.Region)
            .HasDatabaseName("IX_StageEnemies_Region");

        builder.HasIndex(se => new { se.Type, se.Region })
            .HasDatabaseName("IX_StageEnemies_Type_Region");
    }
}
