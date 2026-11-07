using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Task 066: a Logística card's discussion, history and attachments. Everything goes with the
/// card (cascade); a deleted account leaves its comments, history lines and attachments with no author (set null).
/// </summary>
public class LogisticsCardWorkspaceConfiguration :
    IEntityTypeConfiguration<LogisticsCardComment>,
    IEntityTypeConfiguration<LogisticsCardActivity>,
    IEntityTypeConfiguration<LogisticsCardAttachment>
{
    public void Configure(EntityTypeBuilder<LogisticsCardComment> builder)
    {
        builder.Property(c => c.Body).IsRequired().HasMaxLength(2000);
        builder.Property(c => c.AuthorId).HasMaxLength(450);
        builder.HasOne(c => c.Card).WithMany(c => c.Comments).HasForeignKey(c => c.CardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(c => new { c.CardId, c.CreatedAt });
    }

    public void Configure(EntityTypeBuilder<LogisticsCardActivity> builder)
    {
        builder.Property(a => a.Action).IsRequired().HasMaxLength(40);
        builder.Property(a => a.Detail).IsRequired().HasMaxLength(400);
        builder.Property(a => a.ActorId).HasMaxLength(450);
        builder.HasOne(a => a.Card).WithMany(c => c.Activities).HasForeignKey(a => a.CardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.Actor).WithMany().HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(a => new { a.CardId, a.CreatedAt });
    }

    public void Configure(EntityTypeBuilder<LogisticsCardAttachment> builder)
    {
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Url).HasMaxLength(2000);
        builder.Property(a => a.StoragePath).HasMaxLength(500);
        builder.Property(a => a.ContentType).HasMaxLength(100);
        builder.Property(a => a.UploadedById).HasMaxLength(450);
        builder.HasOne(a => a.Card).WithMany(c => c.CardAttachments).HasForeignKey(a => a.CardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.UploadedBy).WithMany().HasForeignKey(a => a.UploadedById).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(a => a.CardId);
    }
}
