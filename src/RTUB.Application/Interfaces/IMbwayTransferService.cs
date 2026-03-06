using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for managing MBWAY transfers
/// </summary>
public interface IMbwayTransferService
{
    /// <summary>
    /// Gets all MBWAY transfers for a specific fiscal year
    /// </summary>
    Task<List<MbwayTransfer>> GetByFiscalYearAsync(int fiscalYearId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all MBWAY transfers
    /// </summary>
    Task<List<MbwayTransfer>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new MBWAY transfer
    /// </summary>
    Task<(bool Success, string Message)> AddTransferAsync(MbwayTransfer transfer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing MBWAY transfer
    /// </summary>
    Task<(bool Success, string Message)> UpdateTransferAsync(MbwayTransfer transfer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a MBWAY transfer
    /// </summary>
    Task<(bool Success, string Message)> DeleteTransferAsync(int transferId, CancellationToken cancellationToken = default);
}
