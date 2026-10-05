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
    Task<EventResult<IReadOnlyList<LogisticsEventDto>>> GetEventsAsync(string? search, ClaimsPrincipal user);
    Task<EventResult<IReadOnlyList<LogisticsMemberOptionDto>>> SearchMembersAsync(string? search, ClaimsPrincipal user);

    Task<EventResult<LogisticsBoardDto>> GetBoardAsync(int id, ClaimsPrincipal user);
    Task<EventResult<LogisticsListDto>> CreateListAsync(int boardId, LogisticsListInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsListDto>> RenameListAsync(int id, LogisticsListInput input, ClaimsPrincipal user);
    Task<EventResult<bool>> MoveListAsync(int id, LogisticsPositionInput input, ClaimsPrincipal user);
    Task<EventResult<bool>> DeleteListAsync(int id, ClaimsPrincipal user);

    Task<EventResult<LogisticsCardDetailDto>> CreateCardAsync(int listId, LogisticsCardCreateInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> GetCardAsync(int id, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> UpdateCardAsync(int id, LogisticsCardInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> SetStatusAsync(int id, LogisticsStatusInput input, ClaimsPrincipal user);
    Task<EventResult<bool>> MoveCardAsync(int id, LogisticsMoveInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> SetLabelsAsync(int id, LogisticsLabelsInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> SetChecklistAsync(int id, LogisticsChecklistInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> SetLinksAsync(int id, LogisticsLinksInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> AddAssignmentAsync(int id, LogisticsAssignmentInput input, ClaimsPrincipal user);
    Task<EventResult<LogisticsCardDetailDto>> RemoveAssignmentAsync(int id, string userId, ClaimsPrincipal user);
    Task<EventResult<bool>> DeleteCardAsync(int id, ClaimsPrincipal user);

    Task<EventResult<LogisticsReminderDto>> CreateReminderAsync(int boardId, LogisticsReminderInput input, ClaimsPrincipal user);

    Task<EventResult<LogisticsFileDto>> UploadFileAsync(int boardId, LogisticsFileUpload upload, ClaimsPrincipal user);
    Task<EventResult<DocumentLinkDto>> GetFileAsync(int boardId, string? name, ClaimsPrincipal user);
    Task<EventResult<bool>> DeleteFileAsync(int boardId, string? name, ClaimsPrincipal user);
}
