using RTUB.Application.DTOs;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for practice session tracking with XP gamification
/// </summary>
public interface IPracticeTrackingService
{
    /// <summary>
    /// Starts a new practice session for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="instrument">Instrument being practiced</param>
    /// <param name="songId">Optional song being practiced</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Session ID</returns>
    Task<int> StartSessionAsync(string userId, InstrumentType instrument, int? songId = null, CancellationToken ct = default);

    /// <summary>
    /// Ends an active practice session and awards XP
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="notes">Optional practice notes</param>
    /// <param name="ct">Cancellation token</param>
    Task EndSessionAsync(int sessionId, string? notes = null, CancellationToken ct = default);

    /// <summary>
    /// Quickly logs a completed practice session (for manual entry)
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="instrument">Instrument practiced</param>
    /// <param name="durationMinutes">Practice duration in minutes</param>
    /// <param name="songId">Optional song practiced</param>
    /// <param name="notes">Optional practice notes</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created session DTO</returns>
    Task<PracticeSessionDto> QuickLogAsync(string userId, InstrumentType instrument, int durationMinutes, int? songId = null, string? notes = null, CancellationToken ct = default);

    /// <summary>
    /// Gets practice statistics for a user within a date range
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="startDate">Optional start date filter</param>
    /// <param name="endDate">Optional end date filter</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Practice statistics</returns>
    Task<PracticeStatsDto> GetStatsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default);

    /// <summary>
    /// Gets paginated list of practice sessions for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of practice sessions</returns>
    Task<List<PracticeSessionDto>> GetSessionsAsync(string userId, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>
    /// Gets the currently active practice session for a user (if any)
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Active session DTO or null if none active</returns>
    Task<PracticeSessionDto?> GetActiveSessionAsync(string userId, CancellationToken ct = default);
}
