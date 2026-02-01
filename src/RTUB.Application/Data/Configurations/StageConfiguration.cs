using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Stage entity
/// Maps domain entity to database schema
/// </summary>
public class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable("Stages");
        
        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.StageNumber)
            .IsRequired();
            
        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(s => s.Description)
            .HasMaxLength(500);
            
        builder.Property(s => s.RequiredLevel)
            .IsRequired();
            
        builder.Property(s => s.EnemyConfigKey)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(s => s.RewardInstrument)
            .IsRequired()
            .HasConversion<int>(); // Store enum as int
            
        builder.Property(s => s.FidelisReward)
            .IsRequired()
            .HasPrecision(18, 2);
            
        builder.Property(s => s.BeerDropChance)
            .IsRequired();
            
        builder.Property(s => s.ShotDropChance)
            .IsRequired();
            
        builder.Property(s => s.IsActive)
            .IsRequired();
            
        builder.Property(s => s.CreatedAt)
            .IsRequired();
            
        // Unique index on stage number
        builder.HasIndex(s => s.StageNumber)
            .IsUnique()
            .HasDatabaseName("IX_Stages_StageNumber");
    }
}
