using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for filtering enrollments
/// Extracted from EventEnrollments.razor to improve separation of concerns
/// </summary>
public interface IEnrollmentFilterService
{
    /// <summary>
    /// Filters enrollments into performing members, leitões, and not attending, with optional search
    /// </summary>
    /// <param name="enrollments">All enrollments to filter</param>
    /// <param name="searchTerm">Optional search term to filter by user name/nickname/email</param>
    /// <returns>Tuple of (performingMembers, leitoes, notAttending)</returns>
    (List<Enrollment> performingMembers, List<Enrollment> leitoes, List<Enrollment> notAttending) FilterEnrollments(
        IEnumerable<Enrollment> enrollments,
        string? searchTerm);
}
