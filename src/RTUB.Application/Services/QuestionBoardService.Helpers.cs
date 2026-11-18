// Question board: remind, delete and the detail, recipient and summary helpers.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public sealed partial class QuestionBoardService
{
    public async Task<EventResult<bool>> RemindAsync(int id, ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        if (await _questions.GetByIdAsync(id) is not { } question)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (!QuestionsAuthorization.CanRemind(question, me))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        await _questions.SendManualReminderAsync(id, me);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        if (await _questions.GetByIdAsync(id) is not { } question)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (!QuestionsAuthorization.IsAuthor(question, me))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        await _questions.DeleteAsync(id, me);
        return EventResult<bool>.Ok(true);
    }

    // ---------- helpers ----------

    private async Task<EventResult<QuestionDetailDto>> DetailAsync(int id, string me)
    {
        if (await _questions.GetByIdWithRepliesAsync(id) is not { } question)
        {
            return EventResult<QuestionDetailDto>.Fail(EventResultStatus.NotFound);
        }

        var replies = question.Replies
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new QuestionReplyDto(r.Id, r.Content, Person(r.Author), r.IsFromAssignedMember, r.CreatedAt))
            .ToList();
        return EventResult<QuestionDetailDto>.Ok(new QuestionDetailDto(Summary(question, me), replies));
    }

    /// <summary>Every (member, position) pair of the Órgãos Sociais, Direção first.</summary>
    private async Task<IReadOnlyList<QuestionRecipientOptionDto>> RecipientsAsync()
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var users = await db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl, u.Positions })
            .ToListAsync();

        return users
            .SelectMany(u => (u.Positions ?? new List<Position>())
                .Where(p => OrgaoSocialHelper.GetGroupForPosition(p).HasValue)
                .Select(p => (User: u, Position: p)))
            .OrderBy(x => OrgaoSocialHelper.GetGroupForPosition(x.Position))
            .ThenBy(x => x.Position)
            .Select(x =>
            {
                var who = GovernanceService.ToMember(x.User.Nickname, x.User.FirstName, x.User.LastName, x.User.ImageUrl);
                return new QuestionRecipientOptionDto(x.User.Id, who.DisplayName, who.FullName, who.AvatarUrl, x.Position.ToString(), Role(x.Position));
            })
            .ToList();
    }

    private static QuestionSummaryDto Summary(Question q, string me)
    {
        var recipient = Person(q.AssignedMember);
        return new QuestionSummaryDto(
            q.Id,
            q.Title,
            q.Content,
            Person(q.Author),
            new QuestionRecipientDto(recipient.DisplayName, recipient.AvatarUrl, Role(q.AssignedPosition)),
            q.Status switch
            {
                QuestionStatus.Answered => "answered",
                QuestionStatus.InDiscussion => "inDiscussion",
                QuestionStatus.Closed => "closed",
                _ => "unanswered",
            },
            q.CreatedAt,
            q.LastActivityAt,
            q.Replies?.Count(r => !r.IsDeleted) ?? 0,
            QuestionsAuthorization.IsAuthor(q, me),
            QuestionsAuthorization.CanReply(q, me),
            QuestionsAuthorization.CanRemind(q, me));
    }

    private static GovernanceMemberDto Person(ApplicationUser? user) =>
        user is null
            ? new GovernanceMemberDto("Membro", null, null)
            : GovernanceService.ToMember(user.Nickname, user.FirstName, user.LastName, user.ImageUrl);

    /// <summary>"Direção - Magister" for the Direção, just the position elsewhere.</summary>
    private static string Role(Position position) =>
        OrgaoSocialHelper.GetGroupForPosition(position) is OrgaoSocialGroup.Direcao
            ? $"{OrgaoSocialHelper.GetDisplayName(OrgaoSocialGroup.Direcao)} - {PositionHelper.GetDisplayName(position)}"
            : PositionHelper.GetDisplayName(position);
}
