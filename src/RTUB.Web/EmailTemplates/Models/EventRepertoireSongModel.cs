namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for a repertoire song in the event reminder email
/// </summary>
public class EventRepertoireSongModel
{
    public string Title { get; set; } = string.Empty;
    public DateTime RepertoireDate { get; set; }
}
