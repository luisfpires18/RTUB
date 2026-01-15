namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for the question notification scheduler
/// </summary>
public class QuestionNotificationOptions
{
    public const string SectionName = "QuestionNotifications";

    /// <summary>
    /// Whether the automatic question notification scheduler is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The times of day to check for unanswered questions and send notifications (format: "HH:mm")
    /// Default is 9:00, 14:00, and 20:00
    /// </summary>
    public List<string> CheckTimes { get; set; } = new() { "09:00", "14:00", "20:00" };
}
