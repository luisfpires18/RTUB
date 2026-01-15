using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a question posted to an Orgãos Sociais member
/// </summary>
public class Question : BaseEntity
{
    [Required]
    [MinLength(10, ErrorMessage = "A pergunta deve ter pelo menos 10 caracteres")]
    public string Content { get; set; } = string.Empty;

    [Required]
    public string AuthorId { get; set; } = string.Empty;

    [Required]
    public Position AssignedPosition { get; set; }

    [Required]
    public string AssignedMemberId { get; set; } = string.Empty;

    [Required]
    public QuestionStatus Status { get; set; } = QuestionStatus.Unanswered;

    /// <summary>
    /// Timestamp of the last notification sent for this question
    /// </summary>
    public DateTime? LastNotificationSent { get; set; }

    /// <summary>
    /// Indicates if the question is awaiting a reply from the user (after member answered)
    /// When true, no notifications should be sent to the assigned member
    /// </summary>
    public bool IsAwaitingUserReply { get; set; }

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual ApplicationUser Author { get; set; } = null!;
    public virtual ApplicationUser AssignedMember { get; set; } = null!;
    public virtual ICollection<QuestionReply> Replies { get; set; } = new List<QuestionReply>();

    // Private constructor for EF Core
    private Question() { }

    /// <summary>
    /// Factory method to create a new question
    /// </summary>
    public static Question Create(string content, string authorId, Position assignedPosition, string assignedMemberId)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required", nameof(content));
        if (content.Length < 10)
            throw new ArgumentException("Content must be at least 10 characters", nameof(content));
        if (string.IsNullOrWhiteSpace(authorId))
            throw new ArgumentException("Author ID is required", nameof(authorId));
        if (string.IsNullOrWhiteSpace(assignedMemberId))
            throw new ArgumentException("Assigned member ID is required", nameof(assignedMemberId));

        return new Question
        {
            Content = content,
            AuthorId = authorId,
            AssignedPosition = assignedPosition,
            AssignedMemberId = assignedMemberId,
            Status = QuestionStatus.Unanswered,
            IsAwaitingUserReply = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks the question as answered and awaiting user reply
    /// </summary>
    public void MarkAsAnswered()
    {
        Status = QuestionStatus.Answered;
        IsAwaitingUserReply = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the question as in discussion (user replied after member answered)
    /// </summary>
    public void MarkAsInDiscussion()
    {
        Status = QuestionStatus.InDiscussion;
        IsAwaitingUserReply = false; // Now awaiting member reply
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the last notification timestamp
    /// </summary>
    public void UpdateLastNotificationSent()
    {
        LastNotificationSent = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft deletes the question
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
