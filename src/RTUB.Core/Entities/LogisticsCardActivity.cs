namespace RTUB.Core.Entities;

/// <summary>
/// One line of a Logística card's history (task 066): who did what, when, already in pt-PT ("Estado: TODO → WIP").
/// Not a <see cref="BaseEntity"/> on purpose: the card history is itself a log, so it is not copied into the global audit
/// log a second time.
/// </summary>
public class LogisticsCardActivity
{
    public int Id { get; set; }

    public int CardId { get; set; }

    public string? ActorId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual LogisticsCard Card { get; set; } = null!;
    public virtual ApplicationUser? Actor { get; set; }
}
