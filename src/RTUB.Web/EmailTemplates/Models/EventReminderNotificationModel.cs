namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for event reminder notification email template
/// </summary>
public class EventReminderNotificationModel
{
    public string EventTitle { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string EventLocation { get; set; } = string.Empty;
    public string EventLink { get; set; } = string.Empty;
    public string EventDescription { get; set; } = string.Empty;
    public string PreferencesLink { get; set; } = "https://rtub.azurewebsites.net/profile";
    public string Nickname { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int DaysUntilEvent { get; set; }
    public List<EventParticipantModel> Participants { get; set; } = new();
    public List<EventRepertoireSongModel> RepertoireSongs { get; set; } = new();
}
