using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a discussion for an event
/// A discussion contains multiple posts
/// </summary>
public class Discussion : BaseEntity
{
    [Required(ErrorMessage = "O evento é obrigatório")]
    public int EventId { get; set; }
    
    [MaxLength(200, ErrorMessage = "O título não pode exceder 200 caracteres")]
    public string? Title { get; set; }
    
    // Navigation properties
    public virtual Event? Event { get; set; }
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
    
    // Private constructor for EF Core
    public Discussion() { }
    
    // Factory method - ensures valid entity creation
    public static Discussion Create(int eventId, string? title = null)
    {
        if (eventId <= 0)
            throw new ArgumentException("O ID do evento deve ser válido", nameof(eventId));
        
        return new Discussion
        {
            EventId = eventId,
            Title = title
        };
    }
    
    // Business methods
    public void UpdateTitle(string? title)
    {
        if (!string.IsNullOrEmpty(title) && title.Length > 200)
            throw new ArgumentException("O título não pode exceder 200 caracteres", nameof(title));
        
        Title = title;
    }
}
