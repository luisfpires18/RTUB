using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for ProductReservation entity
/// Maps domain entity to database schema
/// </summary>
public class ProductReservationConfiguration : IEntityTypeConfiguration<ProductReservation>
{
    public void Configure(EntityTypeBuilder<ProductReservation> builder)
    {
        builder.HasKey(r => r.Id);
        
        builder.Property(r => r.ProductId)
            .IsRequired();
        
        builder.Property(r => r.UserId)
            .IsRequired()
            .HasMaxLength(450);
        
        builder.Property(r => r.UserNickname)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(r => r.DisplayName)
            .HasMaxLength(200);
        
        builder.Property(r => r.HasSizes)
            .IsRequired();
        
        builder.Property(r => r.Size)
            .HasMaxLength(10);
        
        builder.Property(r => r.CreatedAt)
            .IsRequired();
        
        // Relationships
        builder.HasOne(r => r.Product)
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // One active reservation per user per product
        builder.HasIndex(r => new { r.ProductId, r.UserId })
            .IsUnique();
    }
}
