using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a discussion board associated with an event
/// </summary>
public class Discussion : BaseEntity
{
    [Required]
    public int EventId { get; set; }
    
    public virtual Event Event { get; set; } = null!;
    
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
    
    // Private constructor for EF Core
    private Discussion() { }
    
    // Factory method
    public static Discussion Create(int eventId)
    {
        if (eventId <= 0)
            throw new ArgumentException("Event ID must be positive", nameof(eventId));
            
        return new Discussion
        {
            EventId = eventId
        };
    }
}
