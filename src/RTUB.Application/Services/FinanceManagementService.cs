using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing bank and cash values in finance reports
/// Extracted from Report.razor to improve separation of concerns
/// </summary>
public class FinanceManagementService : IFinanceManagementService
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
    public async Task SaveBankCashValueAsync(
        int reportId,
        bool isBank,
        decimal value,
        IActivityService activityService,
        ITransactionService transactionService)
    {
        var activityName = isBank ? "DINHEIRO NO BANCO" : "DINHEIRO EM CAIXA";
        
        // Get or create the activity
        var activities = await activityService.GetActivitiesByReportIdAsync(reportId);
        var currentActivity = activities.FirstOrDefault(a => a.Name == activityName);

        // If activity doesn't exist, create it
        if (currentActivity == null)
        {
            currentActivity = await activityService.CreateActivityAsync(
                reportId,
                activityName,
                DateTime.Today,
                null, // description
                null  // endDate
            );
        }

        // Get existing transactions for this activity
        var existingTransactions = (await transactionService.GetTransactionsByActivityIdAsync(currentActivity.Id)).ToList();
        
        // Best practice: Update existing transaction instead of delete+create to produce a single "Modified" audit log
        if (existingTransactions.Count == 1)
        {
            var existingTransaction = existingTransactions[0];
            
            if (value == 0)
            {
                // Delete the transaction if new value is zero
                await transactionService.DeleteTransactionAsync(existingTransaction.Id);
            }
            else
            {
                // Update the existing transaction (produces single "Modified" audit log)
                var type = value >= 0 ? "Income" : "Expense";
                var amount = Math.Abs(value);
                await transactionService.UpdateTransactionAsync(
                    existingTransaction.Id,
                    DateTime.Today,
                    activityName,
                    "Saldo",
                    amount,
                    type
                );
            }
        }
        else if (existingTransactions.Count > 1)
        {
            // Multiple transactions exist - keep the first one and delete the rest, then update the first
            var transactionToKeep = existingTransactions[0];
            
            // Delete extra transactions
            foreach (var t in existingTransactions.Skip(1))
            {
                await transactionService.DeleteTransactionAsync(t.Id);
            }
            
            if (value == 0)
            {
                // Delete the remaining transaction if new value is zero
                await transactionService.DeleteTransactionAsync(transactionToKeep.Id);
            }
            else
            {
                // Update the remaining transaction
                var type = value >= 0 ? "Income" : "Expense";
                var amount = Math.Abs(value);
                await transactionService.UpdateTransactionAsync(
                    transactionToKeep.Id,
                    DateTime.Today,
                    activityName,
                    "Saldo",
                    amount,
                    type
                );
            }
        }
        else
        {
            // No existing transactions - create a new one if value is non-zero
            if (value != 0)
            {
                var type = value >= 0 ? "Income" : "Expense";
                var amount = Math.Abs(value);
                await transactionService.CreateTransactionAsync(
                    DateTime.Today,
                    activityName,
                    "Saldo",
                    amount,
                    type,
                    currentActivity.Id
                );
            }
        }
    }
}
