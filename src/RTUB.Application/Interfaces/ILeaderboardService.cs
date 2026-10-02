using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>The React /leaderboard (React track 019; was the Blazor "Tabela de Classificação"). Signed-in members.</summary>
public interface ILeaderboardService
{
    /// <summary><paramref name="fiscalYear"/>: "" (every year) or one of the listed years ("2025-2026").</summary>
    Task<EventResult<LeaderboardDto>> GetAsync(string? fiscalYear, string? search, ClaimsPrincipal user);

    Task<EventResult<LeaderboardMemberDto>> GetMemberAsync(string id, string? fiscalYear, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<LeaderboardCommentViewDto>>> GetCommentsAsync(string id, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<LeaderboardCommentViewDto>>> AddCommentAsync(string id, LeaderboardCommentInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> ToggleLikeAsync(int commentId, ClaimsPrincipal user);

    Task<EventResult<bool>> DeleteCommentAsync(int commentId, ClaimsPrincipal user);

    /// <summary>The "ranking_story" text above the levels: Admin and Owner.</summary>
    Task<EventResult<LeaderboardStoryDto>> UpdateStoryAsync(LeaderboardStoryInput input, ClaimsPrincipal user);
}
