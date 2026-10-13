using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>The React /treasury and /treasury/reports/{id} ("Tesouraria", React track 024; were the Blazor /finance pages).</summary>
public interface ITreasuryService
{
    Task<EventResult<TreasuryReportsDto>> GetReportsAsync(ClaimsPrincipal user);
    Task<EventResult<TreasuryReportSummaryDto>> CreateReportAsync(TreasuryReportInput input, ClaimsPrincipal user);
    Task<EventResult<TreasuryReportSummaryDto>> PublishReportAsync(int id, ClaimsPrincipal user);
    Task<EventResult<bool>> DeleteReportAsync(int id, ClaimsPrincipal user);
    Task<EventResult<TreasuryReportDto>> GetReportAsync(int id, ClaimsPrincipal user);
    Task<EventResult<TreasuryFileDto>> GetReportPdfAsync(int id, ClaimsPrincipal user);
    Task<EventResult<TreasuryReportDto>> SetBalanceAsync(int reportId, TreasuryBalanceInput input, ClaimsPrincipal user);

    Task<EventResult<TreasuryReportDto>> CreateActivityAsync(int reportId, TreasuryActivityInput input, ClaimsPrincipal user);
    Task<EventResult<TreasuryReportDto>> UpdateActivityAsync(int id, TreasuryActivityInput input, ClaimsPrincipal user);
    Task<EventResult<TreasuryReportDto>> SetActivityLockAsync(int id, TreasuryLockInput input, ClaimsPrincipal user);
    Task<EventResult<TreasuryReportDto>> DeleteActivityAsync(int id, ClaimsPrincipal user);

    Task<EventResult<TreasuryReportDto>> CreateTransactionAsync(int activityId, TreasuryTransactionInput input, ClaimsPrincipal user);
    Task<EventResult<TreasuryReportDto>> UpdateTransactionAsync(int id, TreasuryTransactionInput input, ClaimsPrincipal user);
    Task<EventResult<TreasuryReportDto>> DeleteTransactionAsync(int id, ClaimsPrincipal user);
    Task<EventResult<TreasuryHistoryDto>> GetHistoryAsync(int reportId, int page, int pageSize, ClaimsPrincipal user);
}
