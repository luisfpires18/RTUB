namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing bank and cash values in finance reports
/// Extracted from Report.razor to improve separation of concerns
/// </summary>
public interface IFinanceManagementService
{
    /// <summary>
    /// Saves or updates the bank or cash value for a report
    /// Handles creating/updating/deleting transactions based on the value
    /// </summary>
    /// <param name="reportId">The report ID</param>
    /// <param name="isBank">True for bank money, false for cash money</param>
    /// <param name="value">The new value (can be negative for expenses)</param>
    /// <param name="activityService">Service for activity operations</param>
    /// <param name="transactionService">Service for transaction operations</param>
    /// <returns>Task representing the async operation</returns>
    Task SaveBankCashValueAsync(
        int reportId,
        bool isBank,
        decimal value,
        IActivityService activityService,
        ITransactionService transactionService);
}
