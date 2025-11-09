using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a member's enrollment in an event
/// </summary>
public class Enrollment : BaseEntity
{
    [Required(ErrorMessage = "O ID do utilizador é obrigatório")]
    public string UserId { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "O ID do evento é obrigatório")]
    [Range(1, int.MaxValue, ErrorMessage = "O ID do evento deve ser maior que 0")]
    public int EventId { get; set; }

    public bool WillAttend { get; set; } = true;
    public InstrumentType? Instrument { get; set; }
    public string? Notes { get; set; }
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Event? Event { get; set; }
    public virtual ApplicationUser? User { get; set; }

    // Private constructor for EF Core
    public Enrollment() { }

    public static Enrollment Create(string userId, int eventId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("O ID do utilizador não pode estar vazio", nameof(userId));

        return new Enrollment
        {
            UserId = userId,
            EventId = eventId
        };
    }
}
