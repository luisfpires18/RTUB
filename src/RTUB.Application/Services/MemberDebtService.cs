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
        // Check if debt already exists for this user and fiscal year
        var existingDebt = await _memberDebtRepository.GetByUserIdAndFiscalYearIdAsync(userId, fiscalYearId);
        if (existingDebt != null)
        {
            // Build descriptive error message with user and fiscal year information
            var userName = existingDebt.User?.FirstName ?? "o utilizador";
            var fiscalYearString = existingDebt.FiscalYear?.GetFiscalYearString() ?? fiscalYearId.ToString();
            throw new InvalidOperationException($"Já existe uma dívida para {userName} no ano fiscal {fiscalYearString}");
        }

        var memberDebt = MemberDebt.Create(userId, amount, description, fiscalYearId);
        return await _memberDebtRepository.AddAsync(memberDebt);
    }

    public async Task UpdateDebtAsync(int debtId, decimal amount, string? description)
    {
        var memberDebt = await _memberDebtRepository.GetByIdAsync(debtId);
        if (memberDebt == null)
            throw new EntityNotFoundException(nameof(MemberDebt), debtId);

        memberDebt.UpdateDetails(amount, description);
        await _memberDebtRepository.UpdateAsync(memberDebt);
    }

    public async Task DeleteDebtAsync(int debtId)
    {
        var memberDebt = await _memberDebtRepository.GetByIdAsync(debtId);
        if (memberDebt == null)
            throw new EntityNotFoundException(nameof(MemberDebt), debtId);

        await _memberDebtRepository.DeleteAsync(memberDebt);
    }

    public async Task<Dictionary<string, decimal>> GetUsersWithDebtsAsync(int fiscalYearId)
    {
        var debts = await _memberDebtRepository.GetByFiscalYearIdAsync(fiscalYearId);
        return debts.ToDictionary(d => d.UserId, d => d.AmountOwed);
    }
}
