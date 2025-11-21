namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for leaderboard comment with like information
/// </summary>
public class LeaderboardCommentDto
{
    public int Id { get; set; }
    public string TargetUserId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorAvatarUrl { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int LikesCount { get; set; }
    public bool IsLikedByCurrentUser { get; set; }
    public bool CanDelete { get; set; }
    public List<string> LikedByNames { get; set; } = new();
}
