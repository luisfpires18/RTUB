// Logistics: card comments.
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class LogisticsKanbanService
{
    /// <summary>Owner, collaborators and managers comment; every viewer reads. Mentions: board viewers only (≤ 20).</summary>
    public Task<EventResult<LogisticsCardDetailDto>> AddCommentAsync(int cardId, LogisticsCommentInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(cardId, user, assignedMayChange: true, async (db, card, access) =>
        {
            if (CommentText(input.Body) is not { } body)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("body", $"Escreva o comentário (máx. {MaxCommentLength} caracteres).");
            }

            var mentions = (input.MentionUserIds ?? Array.Empty<string>()).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
            if (mentions.Count > MaxMentions || (await BoardViewersAsync(db, access.Board, mentions)).Count != mentions.Count)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("mentionUserIds", "Só pode mencionar membros com acesso a este quadro (máx. 20).");
            }

            db.LogisticsCardComments.Add(new LogisticsCardComment
            {
                CardId = card.Id, AuthorId = access.UserId, Body = body, MentionsJson = mentions.Count > 0 ? JsonSerializer.Serialize(mentions) : null,
            });
            // 069: the mentioned members, once, when the comment is posted (an edit never notifies again).
            access.Notices.Add((LogisticsNotice.Mention, mentions.ToArray()));
            return null;
        });

    /// <summary>The author edits their own comment (its mentions stay), while they may still work on the card.</summary>
    public async Task<EventResult<LogisticsCardDetailDto>> EditCommentAsync(int commentId, LogisticsCommentInput input, ClaimsPrincipal user)
    {
        if (await CommentCardAsync(commentId) is not { } cardId)
        {
            return await RefusalAsync<LogisticsCardDetailDto>(user, manage: false) ?? EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
        }

        return await ChangeCardAsync(cardId, user, assignedMayChange: true, async (db, card, access) =>
        {
            if (await db.LogisticsCardComments.FirstOrDefaultAsync(c => c.Id == commentId && c.CardId == card.Id) is not { } comment)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
            }

            if (comment.AuthorId != access.UserId)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Forbidden);
            }

            if (CommentText(input.Body) is not { } body)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("body", $"Escreva o comentário (máx. {MaxCommentLength} caracteres).");
            }

            if (body != comment.Body)
            {
                comment.Body = body;
                comment.EditedAt = DateTime.UtcNow;
            }

            return null;
        });
    }

    /// <summary>
    /// The author deletes their own comment; a manager deletes anyone's (moderation, one history line). Any viewer who
    /// is the author may delete, even after the assignment is gone.
    /// </summary>
    public async Task<EventResult<LogisticsCardDetailDto>> DeleteCommentAsync(int commentId, ClaimsPrincipal user)
    {
        var (refused, db, card, access) = await OpenCardAsync<LogisticsCardDetailDto>(await CommentCardAsync(commentId) ?? 0, user);
        if (refused is not null)
        {
            return refused;
        }

        await using (db)
        {
            if (access!.ReadOnly)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Closed);
            }

            await using var transaction = await db!.Database.BeginTransactionAsync();
            if (await db.LogisticsCardComments.Include(c => c.Author).FirstOrDefaultAsync(c => c.Id == commentId && c.CardId == card!.Id) is not { } comment)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
            }

            var own = comment.AuthorId is not null && comment.AuthorId == access!.UserId;
            if (!own && !access!.Manager)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Forbidden);
            }

            db.LogisticsCardComments.Remove(comment);
            if (!own)
            {
                Log(db, card!.Id, access!, "moderation", $"Comentário de {Person(comment.Author)?.DisplayName ?? "membro removido"} removido");
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return EventResult<LogisticsCardDetailDto>.Ok(await DetailAsync(db, card!.Id, user));
        }
    }

    private async Task<int?> CommentCardAsync(int commentId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.LogisticsCardComments.Where(c => c.Id == commentId).Select(c => (int?)c.CardId).FirstOrDefaultAsync();
    }

    // ---------------------------------------------------------------- the card's own files and links
}