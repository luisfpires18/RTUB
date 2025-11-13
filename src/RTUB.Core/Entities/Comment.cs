using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a comment on a post
/// </summary>
public class Comment : BaseEntity
{
    [Required]
    public int PostId { get; set; }
    
    [Required]
    public string AuthorId { get; set; } = string.Empty;
    
    [Required]
    [MinLength(1, ErrorMessage = "O comentário deve ter pelo menos 1 caractere")]
    public string Body { get; set; } = string.Empty;
    
    public bool IsEdited { get; set; }
    
    public bool IsDeleted { get; set; }
    
    public string? MentionsJson { get; set; }
    
    [Timestamp]
    public byte[]? RowVersion { get; set; }
    
    // Navigation properties
    public virtual Post Post { get; set; } = null!;
    public virtual ApplicationUser Author { get; set; } = null!;
    
    // Private constructor for EF Core
    private Comment() { }
    
    // Factory method
    public static Comment Create(int postId, string authorId, string body)
    {
        if (postId <= 0)
            throw new ArgumentException("Post ID must be positive", nameof(postId));
        if (string.IsNullOrWhiteSpace(authorId))
            throw new ArgumentException("Author ID is required", nameof(authorId));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required", nameof(body));
            
        return new Comment
        {
            PostId = postId,
            AuthorId = authorId,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    public void Edit(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required", nameof(body));
            
        Body = body;
        IsEdited = true;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void SetMentions(string? mentionsJson)
    {
        MentionsJson = mentionsJson;
    }
}
