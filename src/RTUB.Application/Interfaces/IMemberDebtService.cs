using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for MemberDebt operations
/// Provides business logic for managing member debts
/// </summary>
public interface IMemberDebtService
{

    /// <summary>
    /// Adds a new debt for a member
    /// </summary>
    Task<MemberDebt> AddDebtAsync(string userId, decimal amount, string? description, int fiscalYearId, DateTime? compromisedUntil = null);

    /// <summary>
    /// Updates an existing debt
    /// </summary>
    Task UpdateDebtAsync(int debtId, decimal amount, string? description, DateTime? compromisedUntil = null);

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
