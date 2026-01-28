namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for the rehearsal approval reminder scheduler.
/// </summary>
public class RehearsalApprovalReminderOptions
{
    public const string SectionName = "RehearsalApprovalReminder";

    /// <summary>
    /// Whether the rehearsal approval reminder scheduler is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The time of day to send reminders (format: "HH:mm").
    /// Default is 15:00 (3 PM).
    /// </summary>
    public string ScheduledTime { get; set; } = "15:00";

    /// <summary>
    /// The role name that should receive the rehearsal approval reminders.
    /// </summary>
    public string EnsaiadorRoleName { get; set; } = "Ensaiador";
}
