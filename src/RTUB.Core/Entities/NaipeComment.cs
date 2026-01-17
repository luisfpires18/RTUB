using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a comment on a naipe content item (video/image)
/// Allows users to discuss and provide feedback on educational materials
/// </summary>
public class NaipeComment : BaseEntity
{
    [Required]
    public int NaipeContentId { get; set; }

    [Required]
    public string AuthorId { get; set; } = string.Empty;

    [Required]
    [MinLength(1, ErrorMessage = "O comentário deve ter pelo menos 1 caractere")]
    [MaxLength(1000, ErrorMessage = "O comentário não pode exceder 1000 caracteres")]
    public string Text { get; set; } = string.Empty;

    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public virtual NaipeContent NaipeContent { get; set; } = null!;
    public virtual ApplicationUser Author { get; set; } = null!;

    // Helper property to check if deleted
    public bool IsDeleted => DeletedAt.HasValue;

    // Private constructor for EF Core
    private NaipeComment() { }

    // Factory method
    public static NaipeComment Create(int naipeContentId, string authorId, string text)
    {
        if (naipeContentId <= 0)
            throw new ArgumentException("Naipe Content ID must be positive", nameof(naipeContentId));
        if (string.IsNullOrWhiteSpace(authorId))
            throw new ArgumentException("Author ID is required", nameof(authorId));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required", nameof(text));
        if (text.Length > 1000)
            throw new ArgumentException("Text cannot exceed 1000 characters", nameof(text));

        return new NaipeComment
        {
            NaipeContentId = naipeContentId,
            AuthorId = authorId,
            Text = text,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SoftDelete()
    {
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
