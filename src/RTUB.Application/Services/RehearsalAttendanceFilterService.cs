using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for filtering rehearsal attendance records
/// Extracted from Rehearsals.razor to improve separation of concerns
/// </summary>
public class RehearsalAttendanceFilterService : IRehearsalAttendanceFilterService
{
    /// <summary>
    /// Filters attendance records by approval status and search term
    /// </summary>
    /// <param name="attendances">Collection of attendance records to filter</param>
    /// <param name="approvalFilter">Filter by approval status: "Todos", "Pendente", or "Aprovado"</param>
    /// <param name="searchTerm">Search term to filter by user name, nickname, or email</param>
    /// <returns>Filtered attendance records, ordered by CheckedInAt descending</returns>
    public List<RehearsalAttendance> FilterAttendances(
        IEnumerable<RehearsalAttendance> attendances,
        string approvalFilter,
        string searchTerm)
    {
        var filtered = attendances.AsEnumerable();

        // Apply approval status filter
        if (approvalFilter == "Pendente")
        {
            filtered = filtered.Where(a => a.WillAttend && !a.Attended);
        }
        else if (approvalFilter == "Aprovado")
        {
            filtered = filtered.Where(a => a.WillAttend && a.Attended);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.ToLower();
            filtered = filtered.Where(a =>
                (a.User?.FirstName?.ToLower().Contains(search) ?? false) ||
                (a.User?.LastName?.ToLower().Contains(search) ?? false) ||
                (a.User?.Nickname?.ToLower().Contains(search) ?? false) ||
                (a.User?.Email?.ToLower().Contains(search) ?? false));
        }

        return filtered
            .OrderByDescending(a => a.CheckedInAt)
            .ToList();
    }

    /// <summary>
    /// Splits filtered attendances into main participants, leitões, and not attending groups
    /// </summary>
    /// <param name="attendances">Collection of attendance records</param>
    /// <param name="searchTerm">Optional search term to filter not attending members</param>
    /// <returns>Tuple containing (mainParticipants, leitoes, notAttending)</returns>
    public (List<RehearsalAttendance> mainParticipants, List<RehearsalAttendance> leitoes, List<RehearsalAttendance> notAttending)
        SplitAttendancesByCategory(IEnumerable<RehearsalAttendance> attendances, string searchTerm = "")
    {
        // Split into main participants and leitões
        var mainParticipants = attendances
            .Where(a => a.User != null && a.WillAttend && !a.User.Categories.Contains(MemberCategory.Leitao))
            .OrderByDescending(a => a.CheckedInAt)
            .ToList();

        var leitoes = attendances
            .Where(a => a.User != null && a.WillAttend && a.User.Categories.Contains(MemberCategory.Leitao))
            .OrderByDescending(a => a.CheckedInAt)
            .ToList();

        // Filter not attending members with search
        var notAttending = attendances
            .Where(a => a.User != null && !a.WillAttend)
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.ToLower();
            notAttending = notAttending.Where(a =>
                (a.User?.FirstName?.ToLower().Contains(search) ?? false) ||
                (a.User?.LastName?.ToLower().Contains(search) ?? false) ||
                (a.User?.Nickname?.ToLower().Contains(search) ?? false) ||
                (a.User?.Email?.ToLower().Contains(search) ?? false));
        }

        return (mainParticipants, leitoes, notAttending.OrderByDescending(a => a.CheckedInAt).ToList());
    }
}
