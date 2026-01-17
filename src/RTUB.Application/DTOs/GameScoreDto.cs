namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for game score with user and ranking information
/// </summary>
public class GameScoreDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? ProfilePictureSrc { get; set; }
    public string GameId { get; set; } = string.Empty;
    public int Score { get; set; }
    public int Level { get; set; }
    public DateTime PlayedAt { get; set; }
    public int Rank { get; set; }
}
