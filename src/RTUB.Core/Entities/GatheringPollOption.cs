namespace RTUB.Core.Entities;

/// <summary>One candidate date of a <see cref="Gathering"/> poll (local date, 00:00).</summary>
public class GatheringPollOption
{
    public int Id { get; set; }

    public int GatheringId { get; set; }

    public DateTime Date { get; set; }

    public Gathering? Gathering { get; set; }

    public List<GatheringPollVote> Votes { get; set; } = new();
}
