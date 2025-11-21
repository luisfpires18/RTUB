namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for an event participant in the email
/// </summary>
public class EventParticipantModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsLeitao { get; set; }
}
