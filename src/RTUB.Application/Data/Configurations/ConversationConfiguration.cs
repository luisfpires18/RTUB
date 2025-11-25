using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Conversation entity
/// </summary>
public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Participants)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(c => c.LastMessageAt)
            .IsRequired();

        builder.Property(c => c.Title)
            .HasMaxLength(200);

        builder.Property(c => c.IsSystemConversation)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.IsArchived)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        // Relationships
        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(c => c.Participants)
            .HasDatabaseName("IX_Conversation_Participants");

        builder.HasIndex(c => new { c.LastMessageAt, c.IsArchived })
            .HasDatabaseName("IX_Conversation_LastMessageAt_IsArchived");
    }
}
