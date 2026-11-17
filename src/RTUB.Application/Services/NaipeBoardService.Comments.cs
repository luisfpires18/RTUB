// Naipe board: comments on naipe items.
using System.Security.Claims;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class NaipeBoardService
{
    public async Task<EventResult<IReadOnlyList<NaipeCommentItemDto>>> GetCommentsAsync(int id, ClaimsPrincipal user)
    {
        var (_, refused) = await ItemAsync<IReadOnlyList<NaipeCommentItemDto>>(id, user);
        return refused ?? EventResult<IReadOnlyList<NaipeCommentItemDto>>.Ok(await CommentsAsync(id, user));
    }

    public async Task<EventResult<IReadOnlyList<NaipeCommentItemDto>>> AddCommentAsync(int id, NaipeCommentInput input, ClaimsPrincipal user)
    {
        var (_, refused) = await ItemAsync<IReadOnlyList<NaipeCommentItemDto>>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        var text = Clean(input.Text);
        if (text is null)
        {
            return EventResult<IReadOnlyList<NaipeCommentItemDto>>.Invalid("text", "Escreva o comentário.");
        }

        if (text.Length > MaxCommentLength)
        {
            return EventResult<IReadOnlyList<NaipeCommentItemDto>>.Invalid("text", $"O comentário não pode exceder {MaxCommentLength} caracteres.");
        }

        await _naipes.AddCommentAsync(id, NaipesAuthorization.UserId(user)!, text);
        return EventResult<IReadOnlyList<NaipeCommentItemDto>>.Ok(await CommentsAsync(id, user));
    }

    public async Task<EventResult<IReadOnlyList<NaipeCommentItemDto>>> DeleteCommentAsync(int id, int commentId, ClaimsPrincipal user)
    {
        var (_, refused) = await ItemAsync<IReadOnlyList<NaipeCommentItemDto>>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        var me = NaipesAuthorization.UserId(user)!;
        if ((await _naipes.GetCommentsAsync(id, me)).FirstOrDefault(c => c.Id == commentId) is not { } comment)
        {
            return EventResult<IReadOnlyList<NaipeCommentItemDto>>.Fail(EventResultStatus.NotFound);
        }

        if (comment.AuthorId != me && !NaipesAuthorization.CanManage(user))
        {
            return EventResult<IReadOnlyList<NaipeCommentItemDto>>.Fail(EventResultStatus.Forbidden);
        }

        await _naipes.DeleteCommentAsync(commentId, me, NaipesAuthorization.CanManage(user));
        return EventResult<IReadOnlyList<NaipeCommentItemDto>>.Ok(await CommentsAsync(id, user));
    }
}
