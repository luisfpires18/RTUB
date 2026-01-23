using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// MemberDebt service implementation using Repository pattern
/// Contains business logic for member debt operations
/// </summary>
public class MemberDebtService : IMemberDebtService
{
    private readonly IMemberDebtRepository _memberDebtRepository;

    public MemberDebtService(IMemberDebtRepository memberDebtRepository)
    {
        _memberDebtRepository = memberDebtRepository;
    }

    public async Task<IEnumerable<MemberDebt>> GetDebtsForFiscalYearAsync(int fiscalYearId)
    {
        return await _memberDebtRepository.GetByFiscalYearIdAsync(fiscalYearId);
    }

    public async Task<MemberDebt?> GetDebtForUserAsync(string userId, int fiscalYearId)
    {
        return await _memberDebtRepository.GetByUserIdAndFiscalYearIdAsync(userId, fiscalYearId);
    }

    public async Task<MemberDebt> AddDebtAsync(string userId, decimal amount, string? description, int fiscalYearId)
    {
        // No duplicate check - multiple debts per user per fiscal year are now allowed
        var memberDebt = MemberDebt.Create(userId, amount, description, fiscalYearId);
        return await _memberDebtRepository.AddAsync(memberDebt);
    }

    public async Task UpdateDebtAsync(int debtId, decimal amount, string? description)
    {
        var memberDebt = await _memberDebtRepository.GetByIdOrThrowAsync(debtId);

        memberDebt.UpdateDetails(amount, description);
        await _memberDebtRepository.UpdateAsync(memberDebt);
    }

    public async Task DeleteDebtAsync(int debtId)
    {
        var memberDebt = await _memberDebtRepository.GetByIdOrThrowAsync(debtId);

        await _memberDebtRepository.DeleteAsync(memberDebt);
    }

    public async Task<Dictionary<string, decimal>> GetUsersWithDebtsAsync(int fiscalYearId)
    {
        var debts = await _memberDebtRepository.GetByFiscalYearIdAsync(fiscalYearId);
        // Sum all debts per user (multiple debts per user are now allowed)
        return debts
            .GroupBy(d => d.UserId)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.AmountOwed));
    }
}
