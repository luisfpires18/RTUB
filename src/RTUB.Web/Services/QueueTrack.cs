namespace RTUB.Web.Services;

/// <summary>
/// Represents a track in the media playback queue
/// </summary>
public class QueueTrack
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public string? ArtworkUrl { get; set; }
    public int TrackNumber { get; set; }
}
