namespace RTUB.Core.Entities;

/// <summary>
/// Represents a YouTube URL for a song
/// </summary>
public class SongYouTubeUrl : BaseEntity
{
    public int SongId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Navigation property
    public virtual Song? Song { get; set; }

    // Private constructor for EF Core
    public SongYouTubeUrl() { }

    public static SongYouTubeUrl Create(int songId, string url, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("O URL não pode estar vazio", nameof(url));

        return new SongYouTubeUrl
        {
            SongId = songId,
            Url = url,
            Description = description
        };
    }

    public void UpdateUrl(string url, string? description)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("O URL não pode estar vazio", nameof(url));

        Url = url;
        Description = description;
    }
}
