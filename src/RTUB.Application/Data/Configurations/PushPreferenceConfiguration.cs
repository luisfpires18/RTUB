using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class PushPreferenceConfiguration : IEntityTypeConfiguration<PushPreference>
{
    public void Configure(EntityTypeBuilder<PushPreference> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.UserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.Key).HasMaxLength(64).IsRequired();

        // One row per member and key; a send looks the key up across members, the member's own rows by user.
        builder.HasIndex(e => new { e.UserId, e.Key }).IsUnique();
        builder.HasIndex(e => e.Key);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
