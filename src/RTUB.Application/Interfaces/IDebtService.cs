using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for grouping and managing member debts
/// Extracted from Calotes.razor to improve separation of concerns
/// </summary>
public interface IDebtService
{
    /// <summary>
    /// Groups debts by user and calculates total amount owed per user
    /// </summary>
    /// <param name="debts">Collection of member debts</param>
    /// <returns>List of debt groups ordered by total amount (descending)</returns>
    List<MemberDebtGroupDto> GroupDebtsByUser(IEnumerable<MemberDebt> debts);
}
