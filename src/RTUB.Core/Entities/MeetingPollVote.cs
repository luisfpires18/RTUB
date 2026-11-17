using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

public class MeetingPollVote
{
    public int Id { get; set; }

    public int MeetingPollId { get; set; }

    public int MeetingPollOptionId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public DateTime VotedAt { get; set; }

    public MeetingPoll? MeetingPoll { get; set; }

    public MeetingPollOption? Option { get; set; }
}
