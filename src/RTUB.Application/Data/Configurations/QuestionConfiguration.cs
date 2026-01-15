using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Question entity
/// </summary>
public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Content)
            .IsRequired();

        builder.Property(q => q.AuthorId)
            .IsRequired()
            .HasMaxLength(450); // Standard ASP.NET Identity user ID length

        builder.Property(q => q.AssignedPosition)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(q => q.AssignedMemberId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(q => q.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(QuestionStatus.Unanswered);

        builder.Property(q => q.IsAwaitingUserReply)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(q => q.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(q => q.CreatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(q => q.Author)
            .WithMany()
            .HasForeignKey(q => q.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.AssignedMember)
            .WithMany()
            .HasForeignKey(q => q.AssignedMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.Replies)
            .WithOne(r => r.Question)
            .HasForeignKey(r => r.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(q => q.AuthorId)
            .HasDatabaseName("IX_Question_AuthorId");

        builder.HasIndex(q => q.AssignedMemberId)
            .HasDatabaseName("IX_Question_AssignedMemberId");

        builder.HasIndex(q => new { q.Status, q.IsAwaitingUserReply })
            .HasDatabaseName("IX_Question_Status_AwaitingReply");

        builder.HasIndex(q => q.IsDeleted);

        builder.HasIndex(q => q.CreatedAt);
    }
}
