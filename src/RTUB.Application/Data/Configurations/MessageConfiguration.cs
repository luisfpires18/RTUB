using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Message entity
/// </summary>
public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.ConversationId)
            .IsRequired();

        builder.Property(m => m.SenderId)
            .HasMaxLength(450); // Standard ASP.NET Identity user ID length

        builder.Property(m => m.Body)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(m => m.IsSystem)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(m => m.ReadBy)
            .HasMaxLength(4000)
            .HasDefaultValue(string.Empty);

        builder.Property(m => m.Link)
            .HasMaxLength(500);

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for performance
        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt })
            .HasDatabaseName("IX_Message_ConversationId_CreatedAt");

        builder.HasIndex(m => m.SenderId)
            .HasDatabaseName("IX_Message_SenderId");
    }
}
