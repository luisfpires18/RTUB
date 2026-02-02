namespace RTUB.Application.DTOs;

public class MyTunoLeaderboardEntry
{
    public int CharacterId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int Wins { get; set; }
    public int Level { get; set; }
    public int HighestStage { get; set; }
}
