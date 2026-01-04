using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class LoginCountConfiguration : IEntityTypeConfiguration<LoginCount>
{
    public void Configure(EntityTypeBuilder<LoginCount> builder)
    {
        builder.HasKey(lc => lc.Id);

        builder.Property(lc => lc.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(lc => lc.LoginDate)
            .IsRequired();

        builder.Property(lc => lc.Count)
            .IsRequired();

        // Relationships
        builder.HasOne(lc => lc.User)
            .WithMany()
            .HasForeignKey(lc => lc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for querying by user
        builder.HasIndex(lc => lc.UserId);
        
        // Index for querying by date
        builder.HasIndex(lc => lc.LoginDate);
        
        // Unique constraint to ensure only one record per user per day
        builder.HasIndex(lc => new { lc.UserId, lc.LoginDate })
            .IsUnique();
    }
}
