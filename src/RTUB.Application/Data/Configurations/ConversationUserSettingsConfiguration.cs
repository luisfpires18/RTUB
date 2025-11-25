using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for ConversationUserSettings entity
/// </summary>
public class ConversationUserSettingsConfiguration : IEntityTypeConfiguration<ConversationUserSettings>
{
    public void Configure(EntityTypeBuilder<ConversationUserSettings> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ConversationId)
            .IsRequired();

        builder.Property(s => s.UserId)
            .IsRequired()
            .HasMaxLength(450); // ASP.NET Identity GUID length

        builder.Property(s => s.IsMuted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.IsPinned)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(s => s.Conversation)
            .WithMany()
            .HasForeignKey(s => s.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint: one settings record per user per conversation
        builder.HasIndex(s => new { s.ConversationId, s.UserId })
            .IsUnique()
            .HasDatabaseName("IX_ConversationUserSettings_ConversationId_UserId");

        // Index for querying user's settings
        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("IX_ConversationUserSettings_UserId");
    }
}
