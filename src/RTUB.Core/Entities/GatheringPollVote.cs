using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>A member's vote for one date of a <see cref="Gathering"/> date poll: one row per chosen date; a new vote replaces the set.</summary>
public class GatheringPollVote
{
    public int Id { get; set; }

    public int GatheringId { get; set; }

    public int GatheringPollOptionId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public DateTime VotedAt { get; set; }

    public Gathering? Gathering { get; set; }

    public GatheringPollOption? Option { get; set; }
}
