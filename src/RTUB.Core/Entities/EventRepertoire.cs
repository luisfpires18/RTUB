using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a song in an event's repertoire with its display order
/// Join entity for many-to-many relationship between Event and Song
/// </summary>
public class EventRepertoire : BaseEntity
{
    [Required(ErrorMessage = "O evento é obrigatório")]
    public int EventId { get; set; }
    
    [Required(ErrorMessage = "A música é obrigatória")]
    public int SongId { get; set; }
    
    [Required(ErrorMessage = "A ordem de exibição é obrigatória")]
    [Range(1, 1000, ErrorMessage = "A ordem de exibição deve estar entre 1 e 1000")]
    public int DisplayOrder { get; set; }
    
    // Navigation properties
    public virtual Event? Event { get; set; }
    public virtual Song? Song { get; set; }

    // Private constructor for EF Core
    public EventRepertoire() { }

    public static EventRepertoire Create(int eventId, int songId, int displayOrder)
    {
        if (displayOrder < 1)
            throw new ArgumentException("Display order must be greater than 0", nameof(displayOrder));

        return new EventRepertoire
        {
            EventId = eventId,
            SongId = songId,
            DisplayOrder = displayOrder
        };
    }

    public void UpdateOrder(int newOrder)
    {
        if (newOrder < 1)
            throw new ArgumentException("Display order must be greater than 0", nameof(newOrder));
        
        DisplayOrder = newOrder;
    }
}
