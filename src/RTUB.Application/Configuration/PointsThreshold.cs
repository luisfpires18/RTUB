namespace RTUB.Application.Configuration;

/// <summary>
/// Represents a points threshold with associated reward
/// </summary>
public class PointsThreshold
{
    /// <summary>
    /// Minimum points required to reach this threshold
    /// </summary>
    public int MinPoints { get; set; }

    /// <summary>
    /// Additional Fidelis reward for reaching this threshold
    /// </summary>
    public int Reward { get; set; }
}
