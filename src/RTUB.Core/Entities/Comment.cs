using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a comment on a post
/// Users can comment on posts and mention other users
/// </summary>
public class Comment : BaseEntity
{
    [Required(ErrorMessage = "O post é obrigatório")]
    public int PostId { get; set; }
    
    [Required(ErrorMessage = "O utilizador é obrigatório")]
    public string UserId { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "O conteúdo é obrigatório")]
    [MaxLength(2000, ErrorMessage = "O conteúdo não pode exceder 2000 caracteres")]
    public string Content { get; set; } = string.Empty;
    
    // Navigation properties
    public virtual Post? Post { get; set; }
    public virtual ApplicationUser? User { get; set; }
    
    // Private constructor for EF Core
    public Comment() { }
    
    // Factory method - ensures valid entity creation
    public static Comment Create(int postId, string userId, string content)
    {
        if (postId <= 0)
            throw new ArgumentException("O ID do post deve ser válido", nameof(postId));
        
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("O ID do utilizador não pode estar vazio", nameof(userId));
        
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("O conteúdo não pode estar vazio", nameof(content));
        
        if (content.Length > 2000)
            throw new ArgumentException("O conteúdo não pode exceder 2000 caracteres", nameof(content));
        
        return new Comment
        {
            PostId = postId,
            UserId = userId,
            Content = content
        };
    }
    
    // Business methods
    public void UpdateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("O conteúdo não pode estar vazio", nameof(content));
        
        if (content.Length > 2000)
            throw new ArgumentException("O conteúdo não pode exceder 2000 caracteres", nameof(content));
        
        Content = content;
    }
}
