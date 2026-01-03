namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for song learning metadata
/// </summary>
public class SongLearningDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AlbumTitle { get; set; }
    public string? Difficulty { get; set; }
    public string? PrimaryInstrument { get; set; }
    public List<string> SecondaryInstruments { get; set; } = new();
    public List<string> SkillTags { get; set; } = new();
    public int? EstimatedPracticeHours { get; set; }
    public string? YouTubeUrl { get; set; }
    public bool HasTabData { get; set; }
}
