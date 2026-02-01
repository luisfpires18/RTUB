using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for filtering rehearsal attendance records
/// Extracted from Rehearsals.razor to improve separation of concerns
/// </summary>
public interface IRehearsalAttendanceFilterService
{
    /// <summary>
    /// Filters attendance records by approval status and search term
    /// </summary>
    /// <param name="attendances">Collection of attendance records to filter</param>
    /// <param name="approvalFilter">Filter by approval status: "Todos", "Pendente", or "Aprovado"</param>
    /// <param name="searchTerm">Search term to filter by user name, nickname, or email</param>
    /// <returns>Filtered attendance records, ordered by CheckedInAt descending</returns>
    List<RehearsalAttendance> FilterAttendances(
        IEnumerable<RehearsalAttendance> attendances,
        string approvalFilter,
        string searchTerm);

    /// <summary>
    /// Splits filtered attendances into main participants, leitões, and not attending groups
    /// </summary>
    /// <param name="attendances">Collection of attendance records</param>
    /// <param name="searchTerm">Optional search term to filter not attending members</param>
    /// <returns>Tuple containing (mainParticipants, leitoes, notAttending)</returns>
    (List<RehearsalAttendance> mainParticipants, List<RehearsalAttendance> leitoes, List<RehearsalAttendance> notAttending)
        SplitAttendancesByCategory(IEnumerable<RehearsalAttendance> attendances, string searchTerm = "");
}
