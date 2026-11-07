using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class PageViewConfiguration : IEntityTypeConfiguration<PageView>
{
    public void Configure(EntityTypeBuilder<PageView> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Path).HasMaxLength(128).IsRequired();

        // The flush upserts on (Day, Path); the dashboard reads a day range.
        builder.HasIndex(e => new { e.Day, e.Path }).IsUnique();
    }
}
