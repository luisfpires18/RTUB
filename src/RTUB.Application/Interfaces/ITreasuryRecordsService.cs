using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React /treasury/calotes, /treasury/mbway and /treasury/nerba[/{eventId}] ("Tesouraria", React track 024; were the
/// Blazor /calotes, /mbway, /nerba and /nerba/event/{id}).
/// </summary>
public interface ITreasuryRecordsService
{
    Task<EventResult<TreasuryCalotesDto>> GetCalotesAsync(string? fiscalYear, ClaimsPrincipal user);
    Task<EventResult<TreasuryCalotesDto>> AddDebtAsync(TreasuryDebtInput input, ClaimsPrincipal user);
    Task<EventResult<TreasuryCalotesDto>> UpdateDebtAsync(int id, TreasuryDebtInput input, ClaimsPrincipal user);
    Task<EventResult<TreasuryCalotesDto>> DeleteDebtAsync(int id, ClaimsPrincipal user);
    Task<EventResult<TreasuryCalotesDto>> SetCommitmentAsync(TreasuryCommitmentInput input, ClaimsPrincipal user);
    Task<EventResult<IReadOnlyList<TreasuryPersonDto>>> SearchMembersAsync(string? search, bool forDebts, ClaimsPrincipal user);

    Task<EventResult<TreasuryTransfersDto>> GetTransfersAsync(ClaimsPrincipal user);
    Task<EventResult<TreasuryTransfersDto>> AddTransferAsync(TreasuryTransferInput input, ClaimsPrincipal user);
    Task<EventResult<TreasuryTransfersDto>> UpdateTransferAsync(int id, TreasuryTransferInput input, ClaimsPrincipal user);
    Task<EventResult<TreasuryTransfersDto>> DeleteTransferAsync(int id, ClaimsPrincipal user);

    Task<EventResult<TreasuryNerbaDto>> GetNerbaAsync(ClaimsPrincipal user);
    Task<EventResult<TreasuryNerbaDto>> DeleteNerbaOrdersAsync(int eventId, ClaimsPrincipal user);
    Task<EventResult<TreasuryNerbaOrdersDto>> GetNerbaOrdersAsync(int eventId, ClaimsPrincipal user);
    Task<EventResult<TreasuryNerbaOrdersDto>> AddNerbaOrderAsync(int eventId, TreasuryNerbaOrderInput input, ClaimsPrincipal user);
    Task<EventResult<TreasuryNerbaOrdersDto>> UpdateNerbaOrderAsync(int id, TreasuryNerbaOrderInput input, ClaimsPrincipal user);
    Task<EventResult<TreasuryNerbaOrdersDto>> DeleteNerbaOrderAsync(int id, ClaimsPrincipal user);
}
