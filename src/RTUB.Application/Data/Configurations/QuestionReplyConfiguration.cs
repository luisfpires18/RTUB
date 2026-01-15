using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for QuestionReply entity
/// </summary>
public class QuestionReplyConfiguration : IEntityTypeConfiguration<QuestionReply>
{
    public void Configure(EntityTypeBuilder<QuestionReply> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.QuestionId)
            .IsRequired();

        builder.Property(r => r.Content)
            .IsRequired();

        builder.Property(r => r.AuthorId)
            .IsRequired()
            .HasMaxLength(450); // Standard ASP.NET Identity user ID length

        builder.Property(r => r.IsFromAssignedMember)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(r => r.Question)
            .WithMany(q => q.Replies)
            .HasForeignKey(r => r.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Author)
            .WithMany()
            .HasForeignKey(r => r.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for performance
        builder.HasIndex(r => r.QuestionId)
            .HasDatabaseName("IX_QuestionReply_QuestionId");

        builder.HasIndex(r => r.AuthorId)
            .HasDatabaseName("IX_QuestionReply_AuthorId");

        builder.HasIndex(r => r.IsDeleted);

        builder.HasIndex(r => r.CreatedAt);
    }
}
