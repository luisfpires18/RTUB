using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a post in an event discussion
/// </summary>
public class Post : BaseEntity
{
    [Required]
    public int DiscussionId { get; set; }
    
    [Required]
    public string AuthorId { get; set; } = string.Empty;
    
    [Required]
    [MinLength(3, ErrorMessage = "O título deve ter pelo menos 3 caracteres")]
    [MaxLength(120, ErrorMessage = "O título não pode exceder 120 caracteres")]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    [MinLength(3, ErrorMessage = "O conteúdo deve ter pelo menos 3 caracteres")]
    public string Body { get; set; } = string.Empty;
    
    public DateTime LastActivityAt { get; set; }
    
    public bool IsEdited { get; set; }
    
    public bool IsPinned { get; set; }
    
    public bool IsLocked { get; set; }
    
    public bool IsDeleted { get; set; }
    
    public string? MentionsJson { get; set; }
    
    [Timestamp]
    public byte[]? RowVersion { get; set; }
    
    // Navigation properties
    public virtual Discussion Discussion { get; set; } = null!;
    public virtual ApplicationUser Author { get; set; } = null!;
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    
    // Private constructor for EF Core
    private Post() { }
    
    // Factory method
    public static Post Create(int discussionId, string authorId, string title, string body)
    {
        if (discussionId <= 0)
            throw new ArgumentException("Discussion ID must be positive", nameof(discussionId));
        if (string.IsNullOrWhiteSpace(authorId))
            throw new ArgumentException("Author ID is required", nameof(authorId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));
        if (title.Length < 3 || title.Length > 120)
            throw new ArgumentException("Title must be between 3 and 120 characters", nameof(title));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required", nameof(body));
        if (body.Length < 3)
            throw new ArgumentException("Body must be at least 3 characters", nameof(body));
            
        var now = DateTime.UtcNow;
        return new Post
        {
            DiscussionId = discussionId,
            AuthorId = authorId,
            Title = title,
            Body = body,
            LastActivityAt = now,
            CreatedAt = now
        };
    }
    
    public void Edit(string title, string body)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));
        if (title.Length < 3 || title.Length > 120)
            throw new ArgumentException("Title must be between 3 and 120 characters", nameof(title));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required", nameof(body));
        if (body.Length < 3)
            throw new ArgumentException("Body must be at least 3 characters", nameof(body));
            
        Title = title;
        Body = body;
        IsEdited = true;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void Pin()
    {
        IsPinned = true;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void Unpin()
    {
        IsPinned = false;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void Lock()
    {
        IsLocked = true;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void Unlock()
    {
        IsLocked = false;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void UpdateLastActivity()
    {
        LastActivityAt = DateTime.UtcNow;
    }
    
    public void SetMentions(string? mentionsJson)
    {
        MentionsJson = mentionsJson;
    }
}
