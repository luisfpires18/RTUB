namespace RTUB.Application.DTOs;

/// <summary>
/// Data Transfer Object for game scores
/// </summary>
public class GameScoreDto
{
    public int Position { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserNickname { get; set; }
    public string? ProfilePictureSrc { get; set; }
    public int Points { get; set; }
    public int MaxLevel { get; set; }
    public TimeSpan TimeSurvived { get; set; }
    public DateTime SubmittedAt { get; set; }
}
