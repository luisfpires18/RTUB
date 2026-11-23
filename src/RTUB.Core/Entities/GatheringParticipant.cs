using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>A member's Vou / Não vou for one <see cref="Gathering"/>.</summary>
public class GatheringParticipant
{
    public int Id { get; set; }

    public int GatheringId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public bool WillAttend { get; set; }

    public DateTime AnsweredAt { get; set; }

    public Gathering? Gathering { get; set; }
}
