using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>The /trintuna access list: one row per listed member, removed with the account.</summary>
public class TrintunaAccessConfiguration : IEntityTypeConfiguration<TrintunaAccess>
{
    public void Configure(EntityTypeBuilder<TrintunaAccess> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.UserId).IsUnique();
        builder.Property(a => a.UserId).IsRequired().HasMaxLength(450);
        builder.Property(a => a.AddedAt).IsRequired();
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
