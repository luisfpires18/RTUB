using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a reply to a question (either from the assigned member or the question author)
/// </summary>
public class QuestionReply : BaseEntity
{
    [Required]
    public int QuestionId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "A resposta é obrigatória")]
    public string Content { get; set; } = string.Empty;

    [Required]
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>
    /// True if this reply is from the assigned Órgãos Sociais member
    /// </summary>
    public bool IsFromAssignedMember { get; set; }

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual Question Question { get; set; } = null!;
    public virtual ApplicationUser Author { get; set; } = null!;

    // Private constructor for EF Core
    private QuestionReply() { }

    /// <summary>
    /// Factory method to create a new reply
    /// </summary>
    public static QuestionReply Create(int questionId, string content, string authorId, bool isFromAssignedMember)
    {
        if (questionId <= 0)
            throw new ArgumentException("Question ID must be positive", nameof(questionId));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required", nameof(content));
        if (string.IsNullOrWhiteSpace(authorId))
            throw new ArgumentException("Author ID is required", nameof(authorId));

        return new QuestionReply
        {
            QuestionId = questionId,
            Content = content,
            AuthorId = authorId,
            IsFromAssignedMember = isFromAssignedMember,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Soft deletes the reply
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
