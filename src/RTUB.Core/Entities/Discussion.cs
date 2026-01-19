using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a discussion board associated with an event or bet
/// </summary>
public class Discussion : BaseEntity
{
    public int? EventId { get; set; }
    public int? BetId { get; set; }

    public virtual Event? Event { get; set; }
    public virtual Bet? Bet { get; set; }

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

    // Private constructor for EF Core
    private Discussion() { }

    // Factory method for Event discussions
    public static Discussion Create(int eventId)
    {
        if (eventId <= 0)
            throw new ArgumentException("Event ID must be positive", nameof(eventId));

        var discussion = new Discussion
        {
            EventId = eventId,
            BetId = null
        };
        discussion.Validate();
        return discussion;
    }

    // Factory method for Bet discussions
    public static Discussion CreateForBet(int betId)
    {
        if (betId <= 0)
            throw new ArgumentException("Bet ID must be positive", nameof(betId));

        var discussion = new Discussion
        {
            BetId = betId,
            EventId = null
        };
        discussion.Validate();
        return discussion;
    }

    /// <summary>
    /// Validates that exactly one of EventId or BetId is set
    /// </summary>
    private void Validate()
    {
        if (EventId.HasValue && BetId.HasValue)
            throw new InvalidOperationException("Discussion cannot be associated with both an Event and a Bet");
        
        if (!EventId.HasValue && !BetId.HasValue)
            throw new InvalidOperationException("Discussion must be associated with either an Event or a Bet");
    }
}
