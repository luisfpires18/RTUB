using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public sealed class MeetingPollBoardService(
    IDbContextFactory<ApplicationDbContext> contexts,
    TimeProvider time) : IMeetingPollBoardService
{
    private const int MaxQuestionLength = 300;
    private const int MaxOptionLength = 200;
    private const int MinOptions = 2;
    private const int MaxOptions = 12;
    private const string DateFormat = "yyyy-MM-dd'T'HH:mm";

    private static readonly MeetingPollAudience[] AudienceOrder =
    {
        MeetingPollAudience.ConselhoVeteranos,
        MeetingPollAudience.AssembleiaGeral,
        MeetingPollAudience.Direcao,
    };

    public async Task<EventResult<MeetingPollBoardDto>> GetBoardAsync(ClaimsPrincipal principal)
    {
        var (access, refusal) = await AccessAsync(principal);
        if (access is null)
        {
            return EventResult<MeetingPollBoardDto>.Fail(refusal);
        }

        await using var db = await contexts.CreateDbContextAsync();
        var polls = await db.MeetingPolls
            .AsNoTracking()
            .Include(poll => poll.Options)
            .OrderByDescending(poll => poll.CreatedAt)
            .ToListAsync();

        // Votes are counted in SQL: the board never needs the vote rows themselves.
        var counts = await db.MeetingPollVotes
            .GroupBy(vote => vote.MeetingPollOptionId)
            .Select(group => new { OptionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.OptionId, item => item.Count);
        var ownVotes = await db.MeetingPollVotes
            .Where(vote => vote.UserId == access.UserId)
            .ToDictionaryAsync(vote => vote.MeetingPollId, vote => vote.MeetingPollOptionId);

        var now = PortugalTime.Now(time);
        var visible = polls
            .Where(poll => access.CanVoteInPoll(poll.Audience) || access.CanManagePoll(poll.Audience))
            .Select(poll => ToDto(access, poll, now, counts, ownVotes))
            .ToList();
        var createAudiences = AudienceOrder
            .Where(access.CanCreatePoll)
            .Select(audience => new MeetingPollAudienceOptionDto(audience.ToString(), AudienceLabel(audience)))
            .ToList();

        return EventResult<MeetingPollBoardDto>.Ok(new MeetingPollBoardDto(createAudiences, visible));
    }

    public async Task<EventResult<int>> CreateAsync(MeetingPollInput input, ClaimsPrincipal principal)
    {
        var (access, refusal) = await AccessAsync(principal);
        if (access is null)
        {
            return EventResult<int>.Fail(refusal);
        }

        if (!TryAudience(input.Audience, out var audience))
        {
            return EventResult<int>.Invalid(nameof(input.Audience), "Escolha um órgão válido.");
        }

        if (!access.CanCreatePoll(audience))
        {
            return EventResult<int>.Fail(EventResultStatus.Forbidden);
        }

        var question = input.Question?.Trim();
        if (string.IsNullOrEmpty(question) || question.Length > MaxQuestionLength)
        {
            return EventResult<int>.Invalid(nameof(input.Question), $"A pergunta é obrigatória e não pode exceder {MaxQuestionLength} caracteres.");
        }

        if (input.Options is null || input.Options.Count is < MinOptions or > MaxOptions)
        {
            return EventResult<int>.Invalid(nameof(input.Options), $"Indique entre {MinOptions} e {MaxOptions} opções.");
        }

        var options = input.Options.Select(option => option?.Trim() ?? string.Empty).ToList();
        if (options.Any(option => string.IsNullOrEmpty(option) || option.Length > MaxOptionLength))
        {
            return EventResult<int>.Invalid(nameof(input.Options), $"Cada opção é obrigatória e não pode exceder {MaxOptionLength} caracteres.");
        }

        if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
        {
            return EventResult<int>.Invalid(nameof(input.Options), "As opções não podem estar repetidas.");
        }

        if (!TryParseClosingDate(input.ClosesAt, out var closesAt))
        {
            return EventResult<int>.Invalid(nameof(input.ClosesAt), "Indique uma data e hora de fecho válidas e futuras.");
        }

        await using var db = await contexts.CreateDbContextAsync();
        var poll = new MeetingPoll
        {
            Audience = audience,
            Question = question,
            ClosesAt = closesAt,
            Options = options.Select(text => new MeetingPollOption { Text = text }).ToList(),
        };
        db.MeetingPolls.Add(poll);
        await db.SaveChangesAsync();

        return EventResult<int>.Ok(poll.Id);
    }

    public async Task<EventResult<bool>> VoteAsync(int pollId, MeetingPollVoteInput input, ClaimsPrincipal principal)
    {
        var (access, refusal) = await AccessAsync(principal);
        if (access is null)
        {
            return EventResult<bool>.Fail(refusal);
        }

        await using var db = await contexts.CreateDbContextAsync();
        var poll = await db.MeetingPolls
            .Include(item => item.Options)
            .FirstOrDefaultAsync(item => item.Id == pollId);
        if (poll is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        var canVote = access.CanVoteInPoll(poll.Audience);
        if (!canVote)
        {
            return EventResult<bool>.Fail(access.CanManagePoll(poll.Audience)
                ? EventResultStatus.Forbidden
                : EventResultStatus.NotFound);
        }

        if (!IsOpen(poll, PortugalTime.Now(time)))
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        if (!poll.Options.Any(option => option.Id == input.OptionId))
        {
            return EventResult<bool>.Invalid(nameof(input.OptionId), "Escolha uma opção desta votação.");
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var vote = await db.MeetingPollVotes
            .FirstOrDefaultAsync(item => item.MeetingPollId == pollId && item.UserId == userId);
        if (vote is null)
        {
            db.MeetingPollVotes.Add(new MeetingPollVote
            {
                MeetingPollId = pollId,
                MeetingPollOptionId = input.OptionId,
                UserId = userId,
                VotedAt = PortugalTime.Now(time),
            });
        }
        else
        {
            vote.MeetingPollOptionId = input.OptionId;
            vote.VotedAt = PortugalTime.Now(time);
        }

        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> CloseAsync(int pollId, ClaimsPrincipal principal)
    {
        var (access, refusal) = await AccessAsync(principal);
        if (access is null)
        {
            return EventResult<bool>.Fail(refusal);
        }

        await using var db = await contexts.CreateDbContextAsync();
        var poll = await db.MeetingPolls.FirstOrDefaultAsync(item => item.Id == pollId);
        if (poll is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (!access.CanManagePoll(poll.Audience))
        {
            return EventResult<bool>.Fail(access.CanVoteInPoll(poll.Audience)
                ? EventResultStatus.Forbidden
                : EventResultStatus.NotFound);
        }

        var now = PortugalTime.Now(time);
        if (!IsOpen(poll, now))
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        poll.IsClosed = true;
        poll.ClosedAt = now;
        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    private async Task<(MeetingAccess? Access, EventResultStatus Refusal)> AccessAsync(ClaimsPrincipal principal)
    {
        var userId = principal.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        if (string.IsNullOrEmpty(userId))
        {
            return (null, EventResultStatus.SignInRequired);
        }

        await using var db = await contexts.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId);
        if (user is null)
        {
            return (null, EventResultStatus.SignInRequired);
        }

        var access = new MeetingAccess(user, principal.IsInRole("Owner"), principal.IsInRole("Admin"));
        return access.IsBlocked ? (null, EventResultStatus.Forbidden) : (access, EventResultStatus.Ok);
    }

    private static MeetingPollDto ToDto(
        MeetingAccess access,
        MeetingPoll poll,
        DateTime now,
        IReadOnlyDictionary<int, int> counts,
        IReadOnlyDictionary<int, int> ownVotes)
    {
        var eligible = access.CanVoteInPoll(poll.Audience);
        var closed = !IsOpen(poll, now);
        int? ownOptionId = eligible && ownVotes.TryGetValue(poll.Id, out var optionId) ? optionId : null;
        var options = poll.Options
            .OrderBy(option => option.Id)
            .Select(option => new MeetingPollOptionDto(option.Id, option.Text, counts.GetValueOrDefault(option.Id)))
            .ToList();
        var createdAt = PortugalTime.FromUtc(DateTime.SpecifyKind(poll.CreatedAt, DateTimeKind.Utc));

        return new MeetingPollDto(
            poll.Id,
            poll.Audience.ToString(),
            AudienceLabel(poll.Audience),
            poll.Question,
            createdAt.ToString(DateFormat, CultureInfo.InvariantCulture),
            poll.ClosesAt?.ToString(DateFormat, CultureInfo.InvariantCulture),
            closed,
            access.CanManagePoll(poll.Audience),
            eligible && !closed,
            ownOptionId,
            options.Sum(option => option.Votes),
            options);
    }

    private static bool IsOpen(MeetingPoll poll, DateTime now) =>
        !poll.IsClosed && (poll.ClosesAt is null || poll.ClosesAt > now);

    private bool TryParseClosingDate(string? value, out DateTime? closesAt)
    {
        closesAt = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || parsed <= PortugalTime.Now(time))
        {
            return false;
        }

        closesAt = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        return true;
    }

    private static bool TryAudience(string? value, out MeetingPollAudience audience) =>
        Enum.TryParse(value, ignoreCase: false, out audience) && Enum.IsDefined(audience);

    private static string AudienceLabel(MeetingPollAudience audience) => audience switch
    {
        MeetingPollAudience.ConselhoVeteranos => "Conselho de Veteranos",
        MeetingPollAudience.AssembleiaGeral => "Assembleia Geral",
        MeetingPollAudience.Direcao => "Direção",
        _ => throw new ArgumentOutOfRangeException(nameof(audience), audience, "Órgão de votação desconhecido."),
    };
}
