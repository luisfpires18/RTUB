namespace RTUB.Application.DTOs;

// Contracts of the React /leaderboard (React track 019; was the Blazor "Tabela de Classificação"). Signed-in members
// only, as before. What the old page showed: nickname, name, avatar, level, XP and attendance counts; no contact field,
// role or audit field leaves the server.

/// <summary>
/// The table for one fiscal year ("" = every year), positions assigned before the search, as the old page did.
/// <c>Story</c> is the text shown above the levels when active: fixed in code since 029A (no Labels, no editing).
/// </summary>
public sealed record LeaderboardDto(
    IReadOnlyList<MemberOptionDto> FiscalYears,
    string FiscalYear,
    int Total,
    IReadOnlyList<LeaderboardEntryDto> Entries,
    IReadOnlyList<LeaderboardLevelDto> Levels,
    LeaderboardStoryDto? Story);

public sealed record LeaderboardEntryDto(
    int Position,
    string Id,
    string DisplayName,
    string? FullName,
    string? AvatarUrl,
    int Level,
    string RankName,
    int Xp,
    int Rehearsals,
    int Events);

public sealed record LeaderboardLevelDto(int Level, string Name, int XpThreshold);

public sealed record LeaderboardStoryDto(string Title, string Content, bool IsActive);

/// <summary>
/// "Detalhes da Classificação": the level and progress for the selected fiscal year (as the row), the XP origin and the
/// activities of all time (as the old modal).
/// </summary>
public sealed record LeaderboardMemberDto(
    string Id,
    string DisplayName,
    string? FullName,
    string? AvatarUrl,
    LeaderboardProgressDto Progress,
    LeaderboardBreakdownDto Breakdown,
    IReadOnlyList<LeaderboardActivityDto> Activities);

public sealed record LeaderboardProgressDto(
    int Level,
    string RankName,
    int Xp,
    int XpToNextLevel,
    double ProgressPercentage,
    bool IsMaxLevel,
    string? NextRankName);

public sealed record LeaderboardBreakdownDto(
    int TotalXp,
    int RehearsalCount,
    int RehearsalXpPerUnit,
    int RehearsalXpTotal,
    IReadOnlyList<LeaderboardEventXpDto> EventsByType);

public sealed record LeaderboardEventXpDto(string Type, int Count, int XpPerUnit, int TotalXp);

/// <summary><c>Date</c> yyyy-MM-dd.</summary>
public sealed record LeaderboardActivityDto(string Date, string Name, string Type, int XpEarned, bool IsRehearsal);

/// <summary>A comment on a member's classification, newest first. No author id: only what the old card showed.</summary>
public sealed record LeaderboardCommentViewDto(
    int Id,
    string AuthorName,
    string? AuthorAvatarUrl,
    string Text,
    DateTime CreatedAt,
    int Likes,
    bool LikedByMe,
    bool CanDelete,
    IReadOnlyList<string> LikedBy);

public sealed record LeaderboardCommentInput(string? Text);
