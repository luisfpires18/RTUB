using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>EF Core configuration for the shop bundle offers; a product or promotion delete takes its items with it.</summary>
public class ShopPromotionConfiguration : IEntityTypeConfiguration<ShopPromotion>
{
    public void Configure(EntityTypeBuilder<ShopPromotion> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.HasMany(p => p.Items).WithOne(i => i.Promotion).HasForeignKey(i => i.PromotionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShopPromotionItemConfiguration : IEntityTypeConfiguration<ShopPromotionItem>
{
    public void Configure(EntityTypeBuilder<ShopPromotionItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(i => new { i.PromotionId, i.ProductId }).IsUnique();
    }
}
