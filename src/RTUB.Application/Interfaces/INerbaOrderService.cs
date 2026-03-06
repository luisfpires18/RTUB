using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for managing Nerba orders
/// </summary>
public interface INerbaOrderService
{
    /// <summary>
    /// Gets all Nerba orders for a specific report
    /// </summary>
    Task<List<NerbaOrder>> GetByReportIdAsync(int reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all Nerba orders
    /// </summary>
    Task<List<NerbaOrder>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single Nerba order by its identifier
    /// </summary>
    Task<NerbaOrder?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all Nerba orders for a specific event
    /// </summary>
    Task<List<NerbaOrder>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new Nerba order
    /// </summary>
    Task<(bool Success, string Message)> AddOrderAsync(NerbaOrder order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing Nerba order
    /// </summary>
    Task<(bool Success, string Message)> UpdateOrderAsync(NerbaOrder order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a Nerba order
    /// </summary>
    Task<(bool Success, string Message)> DeleteOrderAsync(int orderId, CancellationToken cancellationToken = default);
}
