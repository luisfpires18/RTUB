namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for the birthday email scheduler
/// </summary>
public class BirthdayEmailSchedulerOptions
{
    public const string SectionName = "BirthdayEmailScheduler";

    /// <summary>
    /// Whether the automatic birthday email scheduler is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The time of day to send birthday emails (format: "HH:mm")
    /// Default is 09:00 (9 AM)
    /// </summary>
    public string ScheduledTime { get; set; } = "09:00";
}
