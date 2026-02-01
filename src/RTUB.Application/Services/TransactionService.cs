using System.Text.Json;
using Microsoft.Extensions.Hosting;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;


namespace RTUB.Application.Services;

/// <summary>
/// Transaction service implementation using Repository pattern
/// Contains business logic for transaction operations
/// Now depends on ITransactionRepository abstraction instead of concrete DbContext
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IReceiptStorageService _receiptStorageService;
    private readonly IAuditLogService _auditLogService;
    private readonly IActivityService _activityService;
    private readonly IDocumentationService _documentationService;
    private readonly IHostEnvironment _hostEnvironment;

    public TransactionService(
        ITransactionRepository transactionRepository,
        IReceiptStorageService receiptStorageService,
        IAuditLogService auditLogService,
        IActivityService activityService,
        IDocumentationService documentationService,
        IHostEnvironment hostEnvironment)
    {
        _transactionRepository = transactionRepository;
        _receiptStorageService = receiptStorageService;
        _auditLogService = auditLogService;
        _activityService = activityService;
        _documentationService = documentationService;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<Transaction?> GetTransactionByIdAsync(int id)
    {
        return await _transactionRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Transaction>> GetAllTransactionsAsync()
    {
        return await _transactionRepository.GetAllAsync();
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByActivityIdAsync(int activityId)
    {
        return await _transactionRepository.GetTransactionsByActivityIdAsync(activityId);
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByActivityIdsAsync(IEnumerable<int> activityIds)
    {
        return await _transactionRepository.GetTransactionsByActivityIdsAsync(activityIds);
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(string type)
    {
        return await _transactionRepository.GetTransactionsByTypeAsync(type);
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByUserIdAsync(string userId)
    {
        return await _transactionRepository.GetTransactionsByUserIdAsync(userId);
    }

    public async Task<Transaction> CreateTransactionAsync(DateTime date, string description, string category, decimal amount, string type, int? activityId = null, Stream? receiptStream = null, string? receiptFileName = null, string? receiptContentType = null, string? userId = null)
    {
        var transaction = Transaction.Create(date, description, category, amount, type, activityId, null, userId);
        var createdTransaction = await _transactionRepository.AddAsync(transaction);

        // Upload receipt if provided
        if (receiptStream != null && !string.IsNullOrEmpty(receiptFileName) && !string.IsNullOrEmpty(receiptContentType))
        {
            var receiptUrl = await _receiptStorageService.UploadReceiptAsync(receiptStream, receiptFileName, receiptContentType, createdTransaction.Id);
            createdTransaction.SetReceiptUrl(receiptUrl);
            await _transactionRepository.UpdateAsync(createdTransaction);

            // Index in Documentation system for Tesouraria
            await IndexReceiptInDocumentationAsync(createdTransaction.Id, receiptUrl, receiptStream, receiptFileName, receiptContentType);
        }

        return createdTransaction;
    }

    public async Task UpdateTransactionAsync(int id, DateTime date, string description, string category, decimal amount, string type, Stream? receiptStream = null, string? receiptFileName = null, string? receiptContentType = null, bool deleteReceipt = false, string? userId = null)
    {
        var transaction = await _transactionRepository.GetByIdOrThrowAsync(id);

        transaction.UpdateDetails(date, description, category, amount, type, userId);

        // Handle receipt deletion
        if (deleteReceipt && !string.IsNullOrEmpty(transaction.ReceiptUrl))
        {
            await _receiptStorageService.DeleteReceiptAsync(transaction.ReceiptUrl);
            transaction.SetReceiptUrl(null);
        }
        // Handle receipt replacement
        else if (receiptStream != null && !string.IsNullOrEmpty(receiptFileName) && !string.IsNullOrEmpty(receiptContentType))
        {
            // Delete old receipt if exists
            if (!string.IsNullOrEmpty(transaction.ReceiptUrl))
            {
                await _receiptStorageService.DeleteReceiptAsync(transaction.ReceiptUrl);
            }

            // Upload new receipt
            var receiptUrl = await _receiptStorageService.UploadReceiptAsync(receiptStream, receiptFileName, receiptContentType, id);
            transaction.SetReceiptUrl(receiptUrl);

            // Index in Documentation system for Tesouraria
            await IndexReceiptInDocumentationAsync(id, receiptUrl, receiptStream, receiptFileName, receiptContentType);
        }

        await _transactionRepository.UpdateAsync(transaction);
    }

    public async Task DeleteTransactionAsync(int id)
    {
        var transaction = await _transactionRepository.GetByIdOrThrowAsync(id);

        // Delete receipt from storage if exists
        if (!string.IsNullOrEmpty(transaction.ReceiptUrl))
        {
            await _receiptStorageService.DeleteReceiptAsync(transaction.ReceiptUrl);
        }

        await _transactionRepository.DeleteAsync(transaction);
    }

    public async Task<string?> UploadReceiptAsync(int transactionId, Stream fileStream, string fileName, string contentType)
    {
        var transaction = await _transactionRepository.GetByIdOrThrowAsync(transactionId);

        // Delete old receipt if exists
        if (!string.IsNullOrEmpty(transaction.ReceiptUrl))
        {
            await _receiptStorageService.DeleteReceiptAsync(transaction.ReceiptUrl);
        }

        // Upload new receipt
        var receiptUrl = await _receiptStorageService.UploadReceiptAsync(fileStream, fileName, contentType, transactionId);
        transaction.SetReceiptUrl(receiptUrl);
        await _transactionRepository.UpdateAsync(transaction);

        // Index in Documentation system for Tesouraria
        await IndexReceiptInDocumentationAsync(transactionId, receiptUrl, fileStream, fileName, contentType);

        return receiptUrl;
    }

    /// <summary>
    /// Indexes a receipt in the Documentation system under the Tesouraria special folder
    /// </summary>
    private async Task IndexReceiptInDocumentationAsync(int transactionId, string receiptUrl, Stream fileStream, string fileName, string contentType)
    {
        try
        {
            // Get current fiscal year
            var fiscalYear = FiscalYearHelper.GetCurrentFiscalYearString();

            // Find or create "Tesouraria" special folder
            var folder = await _documentationService.GetOrCreateFolderAsync(
                displayName: "Tesouraria",
                fiscalYear: fiscalYear,
                environment: _hostEnvironment.EnvironmentName,
                isSpecial: true,
                specialVisibility: SpecialVisibility.Tesouraria);

            // Extract object key from receipt URL for duplicate check
            var objectKey = ExtractObjectKeyFromReceiptUrl(receiptUrl);

            if (!string.IsNullOrEmpty(objectKey))
            {
                // Check for duplicate documents by ObjectKey before creating
                // Note: Using a minimal ApplicationUser with isAdmin=true to bypass permission checks
                // GetDocumentsByFolderIdAsync with isAdmin=true short-circuits and doesn't access user properties
                var existingDocuments = await _documentationService.GetDocumentsByFolderIdAsync(
                    folder.Id,
                    new ApplicationUser { Id = "system" },
                    isAdmin: true);

                var documentExists = existingDocuments.Any(d => d.ObjectKey == objectKey);

                if (!documentExists)
                {
                    // Get file size (note: stream position might have changed after upload)
                    long sizeBytes = 0;
                    if (fileStream.CanSeek)
                    {
                        sizeBytes = fileStream.Length;
                    }

                    await _documentationService.CreateDocumentAsync(
                        folderId: folder.Id,
                        displayName: $"Transaction_{transactionId}_{fileName}",
                        cloudflareUrl: receiptUrl,
                        objectKey: objectKey,
                        sizeBytes: sizeBytes,
                        contentType: contentType);
                }
            }
        }
        catch
        {
            // Silently fail - don't break transaction operations if documentation indexing fails
            // The receipt is already uploaded successfully to storage
        }
    }

    /// <summary>
    /// Extracts the object key from a receipt URL
    /// Example: https://pub-xxx.r2.dev/receipts/Production/123_20250101120000.pdf -> receipts/Production/123_20250101120000.pdf
    /// </summary>
    private static string ExtractObjectKeyFromReceiptUrl(string receiptUrl)
    {
        if (string.IsNullOrEmpty(receiptUrl))
            return string.Empty;

        try
        {
            var uri = new Uri(receiptUrl);
            // Remove leading slash if present
            return uri.AbsolutePath.TrimStart('/');
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task DeleteReceiptAsync(int transactionId)
    {
        var transaction = await _transactionRepository.GetByIdOrThrowAsync(transactionId);

        if (!string.IsNullOrEmpty(transaction.ReceiptUrl))
        {
            await _receiptStorageService.DeleteReceiptAsync(transaction.ReceiptUrl);
            transaction.SetReceiptUrl(null);
            await _transactionRepository.UpdateAsync(transaction);
        }
    }

    /// <summary>
    /// Gets transaction history for a report based on audit logs
    /// Simple approach: Get all Transaction audit logs, then check if each transaction belongs to the report
    /// Mirrors AuditLog.razor logic but filters by report activities
    /// </summary>
    public async Task<(IEnumerable<TransactionHistoryEntryDto> entries, int totalCount)> GetTransactionHistoryForReportAsync(int reportId, int page = 1, int pageSize = 10)
    {
        // Get all activities for this report
        var activities = await _activityService.GetActivitiesByReportIdAsync(reportId);
        var activityIds = activities.Select(a => a.Id).ToHashSet();
        var activityNameMap = activities.ToDictionary(a => a.Id, a => a.Name);

        if (!activityIds.Any())
        {
            return (Enumerable.Empty<TransactionHistoryEntryDto>(), 0);
        }

        // Get all Transaction audit logs - simple SELECT * FROM AuditLogs WHERE EntityType = 'Transaction'
        var allLogs = await _auditLogService.GetAllForExportAsync(entityType: "Transaction");

        // Process each log and check if transaction belongs to report
        var historyEntries = new List<TransactionHistoryEntryDto>();

        foreach (var log in allLogs)
        {
            var transactionId = log.EntityId;
            bool belongsToReport = false;
            string activityName = "N/A";
            string? description = null;

            // Strategy 1: If EntityId exists, try to load transaction from database
            if (transactionId.HasValue)
            {
                var transaction = await _transactionRepository.GetByIdAsync(transactionId.Value);
                if (transaction != null && transaction.ActivityId.HasValue && activityIds.Contains(transaction.ActivityId.Value))
                {
                    belongsToReport = true;
                    activityName = activityNameMap.GetValueOrDefault(transaction.ActivityId.Value, "N/A");
                    description = transaction.Description;
                }
            }

            // Strategy 2: Check Changes JSON (handles old Created logs with null EntityId, and Deleted transactions)
            if (!belongsToReport && !string.IsNullOrEmpty(log.Changes))
            {
                try
                {
                    var changes = JsonSerializer.Deserialize<Dictionary<string, object>>(log.Changes);
                    if (changes != null && changes.ContainsKey("ActivityId"))
                    {
                        var extractedActivityId = ExtractActivityIdFromChanges(changes["ActivityId"]);
                        if (extractedActivityId.HasValue && activityIds.Contains(extractedActivityId.Value))
                        {
                            belongsToReport = true;
                            activityName = activityNameMap.GetValueOrDefault(extractedActivityId.Value, "N/A");

                            // Extract Description from Changes
                            if (changes.ContainsKey("Description"))
                            {
                                description = ExtractDescriptionFromChanges(changes["Description"]);
                            }
                        }
                    }
                }
                catch
                {
                    // If parsing fails, skip this log
                    continue;
                }
            }

            // Only process if transaction belongs to this report
            if (!belongsToReport)
            {
                continue;
            }

            // Filter: DINHEIRO EM CAIXA and DINHEIRO NO BANCO should only appear as Modified
            if (activityName.Contains("CAIXA", StringComparison.OrdinalIgnoreCase) ||
                activityName.Contains("BANCO", StringComparison.OrdinalIgnoreCase))
            {
                if (log.Action != "Modified")
                {
                    continue; // Skip Created and Deleted for DINHEIRO activities
                }
            }

            historyEntries.Add(new TransactionHistoryEntryDto
            {
                AuditLogId = log.Id,
                TransactionId = transactionId ?? 0,
                Timestamp = log.Timestamp,
                ActivityName = activityName,
                TransactionDescription = description ?? "N/A",
                UserName = log.UserName ?? "Sistema",
                Action = log.Action
            });
        }

        // Order by latest first (mirroring AuditLog.razor)
        var orderedEntries = historyEntries
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        var totalCount = orderedEntries.Count;

        // Apply pagination
        var paginatedEntries = orderedEntries
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (paginatedEntries, totalCount);
    }

    /// <summary>
    /// Extracts ActivityId from Changes JSON value
    /// Handles both direct values (Created/Deleted) and Old/New structures (Modified)
    /// </summary>
    private static int? ExtractActivityIdFromChanges(object? activityIdValue)
    {
        if (activityIdValue == null) return null;

        if (activityIdValue is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Number)
            {
                return jsonElement.GetInt32();
            }
            else if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                // For Modified actions, ActivityId might be in Old/New structure
                if (jsonElement.TryGetProperty("New", out var newValue) && newValue.ValueKind == JsonValueKind.Number)
                {
                    return newValue.GetInt32();
                }
                else if (jsonElement.TryGetProperty("Old", out var oldValue) && oldValue.ValueKind == JsonValueKind.Number)
                {
                    return oldValue.GetInt32();
                }
            }
        }
        else if (int.TryParse(activityIdValue.ToString(), out var parsedId))
        {
            return parsedId;
        }

        return null;
    }

    /// <summary>
    /// Extracts Description from Changes JSON value
    /// Handles both direct values (Created/Deleted) and Old/New structures (Modified)
    /// </summary>
    private static string? ExtractDescriptionFromChanges(object? descValue)
    {
        if (descValue == null) return null;

        if (descValue is JsonElement descElement)
        {
            if (descElement.ValueKind == JsonValueKind.String)
            {
                return descElement.GetString();
            }
            else if (descElement.ValueKind == JsonValueKind.Object)
            {
                // For Modified actions, Description might be in Old/New structure
                if (descElement.TryGetProperty("New", out var newDesc) && newDesc.ValueKind == JsonValueKind.String)
                {
                    return newDesc.GetString();
                }
                else if (descElement.TryGetProperty("Old", out var oldDesc) && oldDesc.ValueKind == JsonValueKind.String)
                {
                    return oldDesc.GetString();
                }
            }
        }
        else
        {
            return descValue.ToString();
        }

        return null;
    }
}
