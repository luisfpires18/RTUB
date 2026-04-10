using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
{
    public void Configure(EntityTypeBuilder<MessageReaction> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.MessageId).IsRequired();

        builder.Property(r => r.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(r => r.Emoji)
            .IsRequired()
            .HasMaxLength(8); // Emoji can be up to 4 chars but some with modifiers are longer

        builder.Property(r => r.CreatedAt).IsRequired();

        // One reaction per user per message (enforced at DB + service level)
        builder.HasIndex(r => new { r.MessageId, r.UserId })
            .IsUnique()
            .HasDatabaseName("IX_MessageReaction_MessageId_UserId");

        builder.HasOne(r => r.Message)
            .WithMany(m => m.Reactions)
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
