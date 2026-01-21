using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for MemberDebt operations
/// Provides business logic for managing member debts
/// </summary>
public interface IMemberDebtService
{
    /// <summary>
    /// Gets all debts for a specific fiscal year
    /// </summary>
    Task<IEnumerable<MemberDebt>> GetDebtsForFiscalYearAsync(int fiscalYearId);

    /// <summary>
    /// Gets the debt for a specific user in a specific fiscal year
    /// </summary>
    Task<MemberDebt?> GetDebtForUserAsync(string userId, int fiscalYearId);

    /// <summary>
    /// Adds a new debt for a member
    /// </summary>
    Task<MemberDebt> AddDebtAsync(string userId, decimal amount, string? description, int fiscalYearId);

    /// <summary>
    /// Updates an existing debt
    /// </summary>
    Task UpdateDebtAsync(int debtId, decimal amount, string? description);

    /// <summary>
    /// Deletes a debt
    /// </summary>
    Task DeleteDebtAsync(int debtId);

    /// <summary>
    /// Gets users with debts for a specific fiscal year
    /// Returns user IDs and amounts for notification service
    /// </summary>
    Task<Dictionary<string, decimal>> GetUsersWithDebtsAsync(int fiscalYearId);
}
