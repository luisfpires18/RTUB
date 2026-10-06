using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /leaderboard behind the React /leaderboard (React track 019, docs/react-leaderboard.md). Same data and
/// rules, through the services the old page used, now enforced server-side (<see cref="LeaderboardAuthorization"/>):
/// - the table: every account (no category, retired or expelled filter, as before), XP from <see cref="IRankingService"/>
///   (configured XP per attended past rehearsal and per past event the member answered "vai", by event type), level
///   from the configured thresholds; ordered by level then XP (descending), ties in the users table's order; positions
///   assigned before the search; one fiscal year (1 September to 31 August, from 2025-2026) or every year;
/// - the search: nickname, first name, last name or rank name, case-insensitive, as before;
/// - the details: the row's level and progress, the XP origin and the activities of all time;
/// - comments: <see cref="ILeaderboardCommentService"/> (newest first, 1-1000 characters, likes, soft delete by the
///   author or Admin/Owner; the member commented on and the liked author get a push, as before);
/// - the story above the levels: a fixed text in code (<see cref="Story"/>) since 029A removed the Labels admin. It was
///   the "ranking_story" label, edited by Admin and Owner; nobody edits it now. Temporary: the owner decides the final
///   text and how (if at all) it is edited. No schema change.
/// </summary>
public sealed class LeaderboardService : ILeaderboardService
{
    public const int MaxCommentLength = 1000;

    /// <summary>
    /// The story above the levels. Temporary code-backed text (029A): it is the seeded "ranking_story" label's text, so
    /// the page reads as before; the owner replaces it later.
    /// </summary>
    public static readonly LeaderboardStoryDto Story = new(
        "Como Funciona o Sistema de Ranking",
        "O sistema de ranking da RTUB é baseado em XP (Pontos de Experiência) que ganhas ao participar nas atividades da tuna.\n\n🎵 Ensaios: Ganha XP por cada ensaio confirmado a que compareças\n🎭 Atuações: Ganha XP por cada atuação em que participas (o XP varia consoante o tipo de evento)\n🏆 Níveis: À medida que acumulas XP, vais subindo de nível e desbloqueando novos títulos\n\nParticipa ativamente nas atividades da tuna para subires na tabela de classificação e alcançares o nível máximo!",
        true);

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IRankingService _ranking;
    private readonly IMemberStatisticsService _statistics;
    private readonly IFiscalYearService _fiscalYears;
    private readonly ILeaderboardCommentService _comments;
    private readonly ILeaderboardCommentRepository _commentRepository;
    private readonly IOptions<RankingConfiguration> _config;

    public LeaderboardService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IRankingService ranking,
        IMemberStatisticsService statistics,
        IFiscalYearService fiscalYears,
        ILeaderboardCommentService comments,
        ILeaderboardCommentRepository commentRepository,
        IOptions<RankingConfiguration> config)
    {
        _contexts = contexts;
        _ranking = ranking;
        _statistics = statistics;
        _fiscalYears = fiscalYears;
        _comments = comments;
        _commentRepository = commentRepository;
        _config = config;
    }

    public async Task<EventResult<LeaderboardDto>> GetAsync(string? fiscalYear, string? search, ClaimsPrincipal user)
    {
        if (!LeaderboardAuthorization.IsMember(user))
        {
            return EventResult<LeaderboardDto>.Fail(EventResultStatus.SignInRequired);
        }

        var years = await FiscalYearsAsync();
        var selected = fiscalYear ?? "";
        if (selected != "" && !years.Contains(selected))
        {
            return EventResult<LeaderboardDto>.Invalid("fiscalYear", "Ano inválido.");
        }

        var table = await TableAsync(selected);
        var q = search?.Trim().ToLower() ?? "";
        var shown = q == ""
            ? table
            : table.Where(e => (e.DisplayName.ToLower().Contains(q)) || (e.FirstName?.ToLower().Contains(q) ?? false)
                || (e.LastName?.ToLower().Contains(q) ?? false) || e.Entry.RankName.ToLower().Contains(q)).ToList();

        var current = FiscalYearHelper.GetCurrentFiscalYearString();
        return EventResult<LeaderboardDto>.Ok(new LeaderboardDto(
            years.Select(y => new MemberOptionDto(y, y == current ? $"{y} (ATUAL)" : y)).ToList(),
            selected,
            table.Count,
            shown.Select(e => e.Entry).ToList(),
            Levels(),
            Story));
    }

    public async Task<EventResult<LeaderboardMemberDto>> GetMemberAsync(string id, string? fiscalYear, ClaimsPrincipal user)
    {
        if (!LeaderboardAuthorization.IsMember(user))
        {
            return EventResult<LeaderboardMemberDto>.Fail(EventResultStatus.SignInRequired);
        }

        var selected = fiscalYear ?? "";
        if (selected != "" && !(await FiscalYearsAsync()).Contains(selected))
        {
            return EventResult<LeaderboardMemberDto>.Invalid("fiscalYear", "Ano inválido.");
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var member = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (member is null)
        {
            return EventResult<LeaderboardMemberDto>.Fail(EventResultStatus.NotFound);
        }

        var progress = (await ProgressAsync(new[] { id }, selected)).GetValueOrDefault(id)
            ?? new RankProgressInfo { CurrentLevel = 1, CurrentRankName = "Desconhecido" };
        // As the old modal: the XP origin and the activities are of all time, whatever year the table shows.
        var now = DateTime.UtcNow;
        var breakdown = await _statistics.GetUserXpBreakdownAsync(id, now);
        var activities = await _statistics.GetUserAttendedActivitiesAsync(id, now);

        var who = GovernanceService.ToMember(member.Nickname, member.FirstName, member.LastName, member.ImageUrl);
        var next = _config.Value.Levels.FirstOrDefault(l => l.Level == progress.CurrentLevel + 1)?.Name;
        return EventResult<LeaderboardMemberDto>.Ok(new LeaderboardMemberDto(
            member.Id, who.DisplayName, who.FullName, who.AvatarUrl,
            new LeaderboardProgressDto(progress.CurrentLevel, progress.CurrentRankName, progress.CurrentXp, progress.XpToNextLevel,
                progress.ProgressPercentage, progress.IsMaxLevel, progress.IsMaxLevel ? null : next),
            new LeaderboardBreakdownDto(breakdown.TotalXp, breakdown.RehearsalCount, breakdown.RehearsalXpPerUnit, breakdown.RehearsalXpTotal,
                breakdown.EventsByType.Select(e => new LeaderboardEventXpDto(EventTypeLabel(e.TypeName), e.Count, e.XpPerUnit, e.TotalXp)).ToList()),
            activities.Select(a => new LeaderboardActivityDto(a.Date.ToString("yyyy-MM-dd"), a.Name, a.Type, a.XpEarned, a.IsRehearsal)).ToList()));
    }

    public async Task<EventResult<IReadOnlyList<LeaderboardCommentViewDto>>> GetCommentsAsync(string id, ClaimsPrincipal user)
    {
        if (!LeaderboardAuthorization.IsMember(user))
        {
            return EventResult<IReadOnlyList<LeaderboardCommentViewDto>>.Fail(EventResultStatus.SignInRequired);
        }

        return await ExistsAsync(id)
            ? EventResult<IReadOnlyList<LeaderboardCommentViewDto>>.Ok(await CommentsAsync(id, user))
            : EventResult<IReadOnlyList<LeaderboardCommentViewDto>>.Fail(EventResultStatus.NotFound);
    }

    public async Task<EventResult<IReadOnlyList<LeaderboardCommentViewDto>>> AddCommentAsync(string id, LeaderboardCommentInput input, ClaimsPrincipal user)
    {
        if (LeaderboardAuthorization.UserId(user) is not { } me)
        {
            return EventResult<IReadOnlyList<LeaderboardCommentViewDto>>.Fail(EventResultStatus.SignInRequired);
        }

        var text = input.Text?.Trim() ?? "";
        if (text.Length == 0)
        {
            return EventResult<IReadOnlyList<LeaderboardCommentViewDto>>.Invalid("text", "Escreva o comentário.");
        }

        if (text.Length > MaxCommentLength)
        {
            return EventResult<IReadOnlyList<LeaderboardCommentViewDto>>.Invalid("text", "O comentário não pode exceder 1000 caracteres");
        }

        if (!await ExistsAsync(id))
        {
            return EventResult<IReadOnlyList<LeaderboardCommentViewDto>>.Fail(EventResultStatus.NotFound);
        }

        await _comments.AddCommentAsync(id, me, text);
        return EventResult<IReadOnlyList<LeaderboardCommentViewDto>>.Ok(await CommentsAsync(id, user));
    }

    public async Task<EventResult<bool>> ToggleLikeAsync(int commentId, ClaimsPrincipal user)
    {
        if (LeaderboardAuthorization.UserId(user) is not { } me)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        // A deleted comment is never shown, so it cannot be liked either.
        if (await _commentRepository.GetByIdAsync(commentId) is not { DeletedAt: null })
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        return EventResult<bool>.Ok(await _comments.ToggleLikeAsync(commentId, me));
    }

    public async Task<EventResult<bool>> DeleteCommentAsync(int commentId, ClaimsPrincipal user)
    {
        if (LeaderboardAuthorization.UserId(user) is not { } me)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        if (await _commentRepository.GetByIdAsync(commentId) is not { DeletedAt: null } comment)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        var isAdmin = LeaderboardAuthorization.CanManage(user);
        if (!_comments.CanDeleteComment(comment, me, isAdmin))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        await _comments.DeleteCommentAsync(commentId, me, isAdmin);
        return EventResult<bool>.Ok(true);
    }

    // ---------- helpers ----------

    private sealed record Row(LeaderboardEntryDto Entry, string DisplayName, string? FirstName, string? LastName);

    /// <summary>The fiscal years the old filter offered: from the year the app was created, newest first.</summary>
    private async Task<List<string>> FiscalYearsAsync() =>
        (await _fiscalYears.GetAllFiscalYearsAsync())
            .Where(fy => fy.StartYear >= FiscalYearHelper.AppYearCreated)
            .Select(fy => fy.GetFiscalYearString())
            .OrderByDescending(y => y)
            .ToList();

    private static (DateTime Start, DateTime End)? Range(string fiscalYear) =>
        fiscalYear.Split('-') is [var start, _] && int.TryParse(start, out var year)
            ? (new DateTime(year, 9, 1), new DateTime(year + 1, 8, 31, 23, 59, 59))
            : null;

    private Task<Dictionary<string, RankProgressInfo>> ProgressAsync(IEnumerable<string> ids, string fiscalYear) =>
        Range(fiscalYear) is { } range
            ? _ranking.GetRankProgressBatchAsync(ids, range.Start, range.End)
            : _ranking.GetRankProgressBatchAsync(ids);

    /// <summary>The old LoadLeaderboard: every user, level then XP descending, positions 1..n.</summary>
    private async Task<List<Row>> TableAsync(string fiscalYear)
    {
        List<Core.Entities.ApplicationUser> users;
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            users = await db.Users.AsNoTracking().ToListAsync();
        }

        var ids = users.Select(u => u.Id).ToList();
        var progress = await ProgressAsync(ids, fiscalYear);
        Dictionary<string, int> rehearsals;
        List<UserEnrollmentWithEventType> enrollments;
        if (Range(fiscalYear) is { } range)
        {
            rehearsals = await _statistics.GetRehearsalAttendanceCountsByUserAsync(range.Start, range.End);
            enrollments = await _statistics.GetEnrollmentsByUserWithEventTypeAsync(range.Start, range.End);
        }
        else
        {
            var now = DateTime.UtcNow;
            rehearsals = await _statistics.GetRehearsalAttendanceCountsByUserAsync(now);
            enrollments = await _statistics.GetEnrollmentsByUserWithEventTypeAsync(now);
        }

        var events = enrollments.GroupBy(e => e.UserId).ToDictionary(g => g.Key, g => g.Count());
        var ordered = users
            .Select(u => (User: u, Progress: progress.GetValueOrDefault(u.Id, new RankProgressInfo { CurrentLevel = 1, CurrentRankName = "Desconhecido" })))
            .OrderByDescending(x => x.Progress.CurrentLevel)
            .ThenByDescending(x => x.Progress.CurrentXp)
            .ToList();

        return ordered.Select((x, i) =>
        {
            var who = GovernanceService.ToMember(x.User.Nickname, x.User.FirstName, x.User.LastName, x.User.ImageUrl);
            return new Row(
                new LeaderboardEntryDto(i + 1, x.User.Id, who.DisplayName, who.FullName, who.AvatarUrl, x.Progress.CurrentLevel,
                    x.Progress.CurrentRankName, x.Progress.CurrentXp, rehearsals.GetValueOrDefault(x.User.Id), events.GetValueOrDefault(x.User.Id)),
                x.User.Nickname ?? "", x.User.FirstName, x.User.LastName);
        }).ToList();
    }

    private IReadOnlyList<LeaderboardLevelDto> Levels() =>
        _config.Value.Levels.OrderBy(l => l.Level).Select(l => new LeaderboardLevelDto(l.Level, l.Name, l.XpThreshold)).ToList();

    private static string EventTypeLabel(string typeName) =>
        Enum.TryParse<EventType>(typeName, out var type) ? StatusHelper.GetEventTypeDisplay(type) : typeName;

    private async Task<bool> ExistsAsync(string id)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AnyAsync(u => u.Id == id);
    }

    private async Task<IReadOnlyList<LeaderboardCommentViewDto>> CommentsAsync(string id, ClaimsPrincipal user) =>
        (await _comments.GetCommentsForUserAsync(id, LeaderboardAuthorization.UserId(user)))
            .Select(c => new LeaderboardCommentViewDto(c.Id, c.AuthorName, c.AuthorAvatarUrl, c.Text, c.CreatedAt, c.LikesCount,
                c.IsLikedByCurrentUser, c.CanDelete, c.LikedByNames))
            .ToList();
}
