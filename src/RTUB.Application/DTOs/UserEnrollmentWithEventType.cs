using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// DTO representing a user enrollment with associated event type
/// Used for aggregating event participation statistics
/// </summary>
public class UserEnrollmentWithEventType
{
    public string UserId { get; set; } = string.Empty;
    public EventType EventType { get; set; }
}
