using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>EF Core configuration for <see cref="NewsPost"/> (the public "Novidades" feed, React track 025).</summary>
public class NewsPostConfiguration : IEntityTypeConfiguration<NewsPost>
{
    public void Configure(EntityTypeBuilder<NewsPost> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Title).HasMaxLength(NewsPost.TitleMaxLength);
        builder.Property(p => p.Body).IsRequired().HasMaxLength(NewsPost.BodyMaxLength);
        builder.Property(p => p.AuthorId).HasMaxLength(450);

        // A deleted account leaves its posts with no author instead of taking them along.
        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.PublishedAt);
    }
}
