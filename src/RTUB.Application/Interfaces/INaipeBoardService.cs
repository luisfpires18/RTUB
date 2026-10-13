using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React /naipes and /naipes/config (task 033, docs/react-naipes.md; were the Blazor pages): each instrument's
/// teaching videos and images, their comments and play counts, and the instrument settings. Every rule is decided here.
/// </summary>
public interface INaipeBoardService
{
    Task<EventResult<NaipeBoardDto>> GetAsync(string? instrument, string? search, ClaimsPrincipal user);

    Task<EventResult<NaipeItemDto>> CreateAsync(NaipeUpload upload, ClaimsPrincipal user);

    Task<EventResult<NaipeItemDto>> UpdateAsync(int id, NaipeItemInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user);

    /// <summary>A video was played (the old page counted one play per opening of the video).</summary>
    Task<EventResult<bool>> PlayedAsync(int id, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<NaipeCommentItemDto>>> GetCommentsAsync(int id, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<NaipeCommentItemDto>>> AddCommentAsync(int id, NaipeCommentInput input, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<NaipeCommentItemDto>>> DeleteCommentAsync(int id, int commentId, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<NaipeTypeSettingDto>>> GetSettingsAsync(ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<NaipeTypeSettingDto>>> UpdateSettingAsync(int id, NaipeTypeSettingInput input, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<NaipeTypeSettingDto>>> SetPictureAsync(int id, NaipePictureUpload picture, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<NaipeTypeSettingDto>>> RemovePictureAsync(int id, ClaimsPrincipal user);
}
