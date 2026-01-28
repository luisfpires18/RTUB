namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for the bet settlement scheduler
/// </summary>
public class BetSettlementOptions
{
    public const string SectionName = "BetSettlement";

    /// <summary>
    /// Whether the automatic bet settlement scheduler is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The time of day to check and settle bets (format: "HH:mm")
    /// Default is 00:00 (midnight)
    /// </summary>
    public string ScheduledTime { get; set; } = "00:00";
}
