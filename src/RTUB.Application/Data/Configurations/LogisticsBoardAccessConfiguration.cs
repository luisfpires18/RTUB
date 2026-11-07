using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Task 067: a board's responsible member (a plain id: an account deleted reads as none), its access list (one row per member; goes with the
/// board or the account) and its legacy file folder.
/// </summary>
public class LogisticsBoardAccessConfiguration : IEntityTypeConfiguration<LogisticsBoard>, IEntityTypeConfiguration<LogisticsBoardMember>
{
    public void Configure(EntityTypeBuilder<LogisticsBoard> builder)
    {
        builder.Property(b => b.OwnerUserId).HasMaxLength(450);
        builder.Property(b => b.LegacyFolder).HasMaxLength(200);
        // No foreign key on purpose: adding one to an existing SQLite table rebuilds it. A deleted account reads as no owner.
        builder.HasIndex(b => b.OwnerUserId);
    }

    public void Configure(EntityTypeBuilder<LogisticsBoardMember> builder)
    {
        builder.Property(m => m.UserId).IsRequired().HasMaxLength(450);
        builder.HasOne(m => m.Board).WithMany(b => b.Members).HasForeignKey(m => m.BoardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(m => new { m.BoardId, m.UserId }).IsUnique();
    }
}
