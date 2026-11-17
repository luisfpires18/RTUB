using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

public class MeetingPoll : BaseEntity
{
    [Required]
    public MeetingPollAudience Audience { get; set; }

    [Required(ErrorMessage = "A pergunta é obrigatória")]
    [MaxLength(300, ErrorMessage = "A pergunta não pode exceder 300 caracteres")]
    public string Question { get; set; } = string.Empty;

    public DateTime? ClosesAt { get; set; }

    public bool IsClosed { get; set; }

    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// The member who created the poll (may delete it). Null for a poll older than this column whose creator could not
    /// be matched; no foreign key, so removing the member leaves the poll in place.
    /// </summary>
    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public List<MeetingPollOption> Options { get; set; } = new();

    public List<MeetingPollVote> Votes { get; set; } = new();
}
