using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Tracks whether a member has been contacted about an event and their response.
/// </summary>
public class EventContact : BaseEntity
{
    [Required(ErrorMessage = "O ID do evento é obrigatório")]
    [Range(1, int.MaxValue, ErrorMessage = "O ID do evento deve ser maior que 0")]
    public int EventId { get; set; }

    [Required(ErrorMessage = "O ID do utilizador é obrigatório")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the member has been contacted.
    /// </summary>
    public bool IsContacted { get; set; }

    /// <summary>
    /// Whether the member will attend (null = undecided).
    /// </summary>
    public bool? WillAttend { get; set; }

    /// <summary>
    /// Notes about the contact/conversation.
    /// </summary>
    [MaxLength(500, ErrorMessage = "As notas não podem exceder 500 caracteres")]
    public string? Notes { get; set; }

    /// <summary>
    /// When the member was contacted.
    /// </summary>
    public DateTime? ContactedAt { get; set; }

    /// <summary>
    /// Who contacted the member (user ID).
    /// </summary>
    public string? ContactedBy { get; set; }

    // Navigation properties
    public virtual Event? Event { get; set; }
    public virtual ApplicationUser? User { get; set; }

    public EventContact() { }

    /// <summary>
    /// Creates a new EventContact record.
    /// </summary>
    public static EventContact Create(int eventId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("O ID do utilizador não pode estar vazio", nameof(userId));

        return new EventContact
        {
            EventId = eventId,
            UserId = userId
        };
    }

    /// <summary>
    /// Marks this contact as contacted with optional attendance and notes.
    /// </summary>
    public void MarkAsContacted(bool? willAttend, string? notes, string contactedBy)
    {
        IsContacted = true;
        WillAttend = willAttend;
        Notes = notes;
        ContactedAt = DateTime.UtcNow;
        ContactedBy = contactedBy;
    }

    /// <summary>
    /// Resets the contact status.
    /// </summary>
    public void ResetContact()
    {
        IsContacted = false;
        WillAttend = null;
        Notes = null;
        ContactedAt = null;
        ContactedBy = null;
    }
}
