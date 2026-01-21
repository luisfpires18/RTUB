using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for MemberDebt entity with domain-specific operations
/// </summary>
public interface IMemberDebtRepository : IRepository<MemberDebt>
{
    /// <summary>
    /// Gets all debts for a specific fiscal year
    /// </summary>
    Task<IEnumerable<MemberDebt>> GetByFiscalYearIdAsync(int fiscalYearId);

    /// <summary>
    /// Gets all debts for a specific user across all fiscal years
    /// </summary>
    Task<IEnumerable<MemberDebt>> GetByUserIdAsync(string userId);

    /// <summary>
    /// Gets debt for a specific user in a specific fiscal year
    /// </summary>
    Task<MemberDebt?> GetByUserIdAndFiscalYearIdAsync(string userId, int fiscalYearId);
}
