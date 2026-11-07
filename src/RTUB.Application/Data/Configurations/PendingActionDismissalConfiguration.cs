using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class PendingActionDismissalConfiguration : IEntityTypeConfiguration<PendingActionDismissal>
{
    public void Configure(EntityTypeBuilder<PendingActionDismissal> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.UserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.ActionKind).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SourceKey).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Period).HasMaxLength(32).IsRequired();

        // One dismissal per member, action, source and period: a retried or double-clicked "não" is the same row.
        // The member's own rows are read by this index too. Rows of a deleted account go with it.
        builder.HasIndex(e => new { e.UserId, e.ActionKind, e.SourceKey, e.Period }).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
