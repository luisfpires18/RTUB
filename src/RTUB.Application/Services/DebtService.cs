using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for grouping and managing member debts
/// Extracted from Calotes.razor to improve separation of concerns
/// </summary>
public class DebtService : IDebtService
{
    /// <summary>
    /// Groups debts by user and calculates total amount owed per user
    /// </summary>
    /// <param name="debts">Collection of member debts</param>
    /// <returns>List of debt groups ordered by total amount (descending)</returns>
    public List<MemberDebtGroupDto> GroupDebtsByUser(IEnumerable<MemberDebt> debts)
    {
        return debts
            .GroupBy(d => d.UserId)
            .Select(g => new MemberDebtGroupDto
            {
                User = g.First().User,
                Debts = g.OrderByDescending(d => d.AmountOwed).ToList()
            })
            .OrderByDescending(g => g.TotalAmount)
            .ToList();
    }
}
