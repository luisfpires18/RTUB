using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

// Meeting polls: create a poll and notify its audience.
public sealed partial class MeetingPollBoardService
{
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
            CreatedByUserId = access.UserId,
            Options = options.Select(text => new MeetingPollOption { Text = text }).ToList(),
        };
        db.MeetingPolls.Add(poll);
        await db.SaveChangesAsync();

        await NotifyAudienceAsync(db, poll);
        return EventResult<int>.Ok(poll.Id);
    }

    /// <summary>
    /// Push to the members who may vote in the poll: the same rule the board and the vote use
    /// (<see cref="MeetingAccess.CanVoteInPoll"/>), so nobody outside the audience learns the question. Owner is not a
    /// voter by role. A failed push never fails the creation.
    /// </summary>
    private async Task NotifyAudienceAsync(ApplicationDbContext db, MeetingPoll poll)
    {
        try
        {
            var users = await db.Users.AsNoTracking().ToListAsync();
            var admins = (await (from link in db.UserRoles
                                 join role in db.Roles on link.RoleId equals role.Id
                                 where role.Name == "Admin"
                                 select link.UserId).ToListAsync()).ToHashSet();
            var recipients = users
                .Where(user => new MeetingAccess(user, isOwner: false, isAdmin: admins.Contains(user.Id)).CanVoteInPoll(poll.Audience))
                .Select(user => user.Id)
                .ToList();

            await push.SendToSelectedUsersAsync(recipients, pushFactory.CreateMeetingPollNotification(poll, AudienceLabel(poll.Audience)));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send the new poll push for poll {PollId}", poll.Id);
        }
    }
}
