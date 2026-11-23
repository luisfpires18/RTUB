using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// A Trintuna gathering ("Nº Almoço", docs/react-trintuna.md). <see cref="Date"/> is a local date (00:00), set only by
/// settling the poll with one of its own options. <see cref="IsHistorical"/> is true only on the seeded rows: always "Passados".
/// </summary>
public class Gathering : BaseEntity
{
    [Range(1, int.MaxValue)]
    public int Number { get; set; }

    [Required]
    [MaxLength(200)]
    public string Place { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(450)]
    public string? OrganizerUserId { get; set; }

    public DateTime? Date { get; set; }

    public bool IsHistorical { get; set; }

    public virtual ApplicationUser? Organizer { get; set; }

    public List<GatheringPollOption> PollOptions { get; set; } = new();

    public List<GatheringPollVote> PollVotes { get; set; } = new();

    public List<GatheringParticipant> Participants { get; set; } = new();
}
