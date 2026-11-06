namespace RTUB.Core.Entities;

/// <summary>
/// A comment in a Logística card's discussion (task 066). Plain text, never HTML. <c>MentionsJson</c> holds the ids of the
/// members it mentions (validated as board viewers when written; notifications come in phase 4). The author may be gone
/// (account deleted): <c>AuthorId</c> is then null.
/// </summary>
public class LogisticsCardComment : BaseEntity
{
    public int CardId { get; set; }

    public string? AuthorId { get; set; }

    public string Body { get; set; } = string.Empty;

    public string? MentionsJson { get; set; }

    public DateTime? EditedAt { get; set; }

    public virtual LogisticsCard Card { get; set; } = null!;
    public virtual ApplicationUser? Author { get; set; }
}
