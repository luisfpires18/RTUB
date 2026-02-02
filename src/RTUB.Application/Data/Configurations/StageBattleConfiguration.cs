using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for StageBattle entity
/// Maps domain entity to database schema
/// </summary>
public class StageBattleConfiguration : IEntityTypeConfiguration<StageBattle>
{
    public void Configure(EntityTypeBuilder<StageBattle> builder)
    {
        builder.HasKey(sb => sb.Id);

        builder.Property(sb => sb.CharacterId)
            .IsRequired();

        builder.Property(sb => sb.StageNumber)
            .IsRequired();

        builder.Property(sb => sb.EnemyType)
            .IsRequired();

        builder.Property(sb => sb.Region)
            .IsRequired();

        builder.Property(sb => sb.EnemyName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(sb => sb.Seed)
            .IsRequired();

        builder.Property(sb => sb.Outcome)
            .IsRequired();

        builder.Property(sb => sb.XPReward)
            .IsRequired();

        builder.Property(sb => sb.FidelisReward)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(sb => sb.BeersDropped)
            .IsRequired();

        builder.Property(sb => sb.ShotsDropped)
            .IsRequired();

        builder.Property(sb => sb.ReplayJson)
            .IsRequired();

        builder.Property(sb => sb.CreatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(sb => sb.CharacterId)
            .HasDatabaseName("IX_StageBattles_CharacterId");

        builder.HasIndex(sb => sb.StageNumber)
            .HasDatabaseName("IX_StageBattles_StageNumber");

        builder.HasIndex(sb => new { sb.CharacterId, sb.CreatedAt })
            .HasDatabaseName("IX_StageBattles_CharacterId_CreatedAt");

        // Relationships
        builder.HasOne(sb => sb.Character)
            .WithMany()
            .HasForeignKey(sb => sb.CharacterId)
            .OnDelete(DeleteBehavior.Cascade); // Delete battles when character is deleted

        builder.HasOne(sb => sb.StageEnemy)
            .WithMany()
            .HasForeignKey(sb => sb.StageEnemyId)
            .OnDelete(DeleteBehavior.SetNull); // Set to null if enemy template is deleted
    }
}
