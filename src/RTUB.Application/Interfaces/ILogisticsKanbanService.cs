using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>The React /logistics and /logistics/{id} ("Logística", React track 023; were the Blazor pages).</summary>
public interface ILogisticsKanbanService
{
    Task<EventResult<LogisticsBoardsDto>> GetBoardsAsync(string? search, ClaimsPrincipal user);
    Task<EventResult<LogisticsBoardSummaryDto>> CreateBoardAsync(LogisticsBoardInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsBoardSummaryDto>> UpdateBoardAsync(int id, LogisticsBoardInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsBoardSummaryDto>> SetBoardStateAsync(int id, LogisticsBoardStateInput input, ClaimsPrincipal user);
    Task<EventResult<bool>> DeleteBoardAsync(int id, ClaimsPrincipal user);
    // Task 067: archive, copies and templates
    Task<EventResult<LogisticsBoardSummaryDto>> ArchiveBoardAsync(int id, LogisticsArchiveInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsBoardSummaryDto>> DuplicateBoardAsync(int id, LogisticsDuplicateInput input, ClaimsPrincipal user);
    Task<EventResult<IReadOnlyList<LogisticsTemplateDto>>> GetTemplatesAsync(ClaimsPrincipal user);
    Task<EventResult<LogisticsBoardSummaryDto>> ComposeBoardAsync(LogisticsComposeInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> ArchiveCardAsync(int id, LogisticsArchiveInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> DuplicateCardAsync(int id, ClaimsPrincipal user);
    Task<EventResult<IReadOnlyList<LogisticsEventDto>>> GetEventsAsync(string? search, ClaimsPrincipal user);
    Task<EventResult<IReadOnlyList<LogisticsMemberOptionDto>>> SearchMembersAsync(string? search, ClaimsPrincipal user, int? boardId = null);

    /// <summary><paramref name="includeFiles"/> false leaves <c>Files</c> empty: the file listing is a storage round trip, so the page asks for it separately (<see cref="GetFilesAsync"/>).</summary>
    Task<EventResult<LogisticsBoardDto>> GetBoardAsync(int id, ClaimsPrincipal user, bool includeFiles = true);

    /// <summary>The files of one board (a storage listing).</summary>
    Task<EventResult<IReadOnlyList<LogisticsFileDto>>> GetFilesAsync(int boardId, ClaimsPrincipal user);
    Task<EventResult<LogisticsListDto>> CreateListAsync(int boardId, LogisticsListInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsListDto>> RenameListAsync(int id, LogisticsListInput input, ClaimsPrincipal user);
    Task<EventResult<bool>> MoveListAsync(int id, LogisticsPositionInput input, ClaimsPrincipal user);
    Task<EventResult<bool>> DeleteListAsync(int id, int? moveTo, ClaimsPrincipal user);

    Task<EventResult<LogisticsCardDetailDto>> CreateCardAsync(int listId, LogisticsCardCreateInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> GetCardAsync(int id, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> UpdateCardAsync(int id, LogisticsCardInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> SetStatusAsync(int id, LogisticsStatusInput input, ClaimsPrincipal user);
    Task<EventResult<bool>> MoveCardAsync(int id, LogisticsMoveInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> SetLabelsAsync(int id, LogisticsLabelsInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> AddChecklistItemAsync(int id, LogisticsChecklistItemInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> RenameChecklistItemAsync(int id, int index, LogisticsChecklistItemInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> RemoveChecklistItemAsync(int id, int index, string? expected, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> ToggleChecklistItemAsync(int id, int index, LogisticsChecklistToggleInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> SetLinksAsync(int id, LogisticsLinksInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> AddAssignmentAsync(int id, LogisticsAssignmentInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> RemoveAssignmentAsync(int id, string userId, ClaimsPrincipal user);
    Task<EventResult<bool>> DeleteCardAsync(int id, ClaimsPrincipal user);

    // Task 066: the card's discussion and attachments
    Task<EventResult<LogisticsCardDetailDto>> AddCommentAsync(int cardId, LogisticsCommentInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> EditCommentAsync(int commentId, LogisticsCommentInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> DeleteCommentAsync(int commentId, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> UploadAttachmentAsync(int cardId, LogisticsFileUpload upload, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> AddLinkAsync(int cardId, LogisticsLinkInput input, ClaimsPrincipal user);
    Task<EventResult<DocumentLinkDto>> GetAttachmentAsync(int cardId, int attachmentId, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> DeleteAttachmentAsync(int cardId, int attachmentId, ClaimsPrincipal user);

    Task<EventResult<LogisticsReminderDto>> CreateReminderAsync(int boardId, LogisticsReminderInput input, ClaimsPrincipal user);

    Task<EventResult<LogisticsFileDto>> UploadFileAsync(int boardId, LogisticsFileUpload upload, ClaimsPrincipal user);
    Task<EventResult<DocumentLinkDto>> GetFileAsync(int boardId, string? fileId, ClaimsPrincipal user);
    Task<EventResult<bool>> DeleteFileAsync(int boardId, string? fileId, ClaimsPrincipal user);
}
