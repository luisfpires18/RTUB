using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Instrument entity
/// </summary>
public class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.HasKey(i => i.Id);
        
        builder.Property(i => i.Category)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(i => i.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(i => i.SerialNumber)
            .HasMaxLength(100);
        
        builder.Property(i => i.Brand)
            .HasMaxLength(100);
        
        builder.Property(i => i.Location)
            .HasMaxLength(200);
        
        builder.Property(i => i.MaintenanceNotes)
            .HasMaxLength(500);
        
        builder.Property(i => i.ImageUrl)
            .HasMaxLength(500);
        
        builder.Property(i => i.Condition)
            .IsRequired();
        
        builder.Property(i => i.CreatedAt)
            .IsRequired();
    }
}
