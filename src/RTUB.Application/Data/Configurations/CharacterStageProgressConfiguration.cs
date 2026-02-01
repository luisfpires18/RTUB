using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for CharacterStageProgress entity
/// Maps domain entity to database schema
/// </summary>
public class CharacterStageProgressConfiguration : IEntityTypeConfiguration<CharacterStageProgress>
{
    public void Configure(EntityTypeBuilder<CharacterStageProgress> builder)
    {
        builder.ToTable("CharacterStageProgress");
        
        builder.HasKey(csp => csp.Id);
        
        builder.Property(csp => csp.CharacterId)
            .IsRequired();
            
        builder.Property(csp => csp.StageId)
            .IsRequired();
            
        builder.Property(csp => csp.CompletionCount)
            .IsRequired();
            
        builder.Property(csp => csp.InstrumentClaimed)
            .IsRequired();
            
        builder.Property(csp => csp.CreatedAt)
            .IsRequired();
            
        // One-to-many: Character has many stage progress records
        builder.HasOne(csp => csp.Character)
            .WithMany()
            .HasForeignKey(csp => csp.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // One-to-many: Stage has many progress records
        builder.HasOne(csp => csp.Stage)
            .WithMany()
            .HasForeignKey(csp => csp.StageId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Unique constraint: one progress record per character per stage
        builder.HasIndex(csp => new { csp.CharacterId, csp.StageId })
            .IsUnique()
            .HasDatabaseName("IX_CharacterStageProgress_CharacterId_StageId");
            
        // Index for querying by character
        builder.HasIndex(csp => csp.CharacterId)
            .HasDatabaseName("IX_CharacterStageProgress_CharacterId");
    }
}
