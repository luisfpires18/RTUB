namespace RTUB.Application.DTOs;

/// <summary>
/// A performance request from the public, as both the Blazor /request page and the React
/// POST /api/public/requests hand it to <see cref="Interfaces.IPublicRequestService"/>.
/// Maps onto the existing <see cref="Core.Entities.Request"/>; no persisted field is added.
/// </summary>
public sealed record PublicRequestSubmission(
    string Name,
    string Email,
    string Phone,
    string EventType,
    DateTime PreferredDate,
    bool IsDateRange,
    DateTime? PreferredEndDate,
    string Location,
    string? Message);
