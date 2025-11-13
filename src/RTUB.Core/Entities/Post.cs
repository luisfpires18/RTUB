using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a post in a discussion
/// Users can create posts and mention other users
/// </summary>
public class Post : BaseEntity
{
    [Required(ErrorMessage = "A discussão é obrigatória")]
    public int DiscussionId { get; set; }
    
    [Required(ErrorMessage = "O utilizador é obrigatório")]
    public string UserId { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "O conteúdo é obrigatório")]
    [MaxLength(5000, ErrorMessage = "O conteúdo não pode exceder 5000 caracteres")]
    public string Content { get; set; } = string.Empty;
    
    // Navigation properties
    public virtual Discussion? Discussion { get; set; }
    public virtual ApplicationUser? User { get; set; }
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    
    // Private constructor for EF Core
    public Post() { }
    
    // Factory method - ensures valid entity creation
    public static Post Create(int discussionId, string userId, string content)
    {
        if (discussionId <= 0)
            throw new ArgumentException("O ID da discussão deve ser válido", nameof(discussionId));
        
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("O ID do utilizador não pode estar vazio", nameof(userId));
        
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("O conteúdo não pode estar vazio", nameof(content));
        
        if (content.Length > 5000)
            throw new ArgumentException("O conteúdo não pode exceder 5000 caracteres", nameof(content));
        
        return new Post
        {
            DiscussionId = discussionId,
            UserId = userId,
            Content = content
        };
    }
    
    // Business methods
    public void UpdateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("O conteúdo não pode estar vazio", nameof(content));
        
        if (content.Length > 5000)
            throw new ArgumentException("O conteúdo não pode exceder 5000 caracteres", nameof(content));
        
        Content = content;
    }
}
