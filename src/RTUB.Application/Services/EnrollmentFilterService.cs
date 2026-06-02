using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for filtering enrollments
/// Extracted from EventEnrollments.razor to improve separation of concerns
/// </summary>
public class EnrollmentFilterService : IEnrollmentFilterService
{
    public (List<Enrollment> performingMembers, List<Enrollment> leitoes, List<Enrollment> notAttending) FilterEnrollments(
        IEnumerable<Enrollment> enrollments,
        string? searchTerm)
    {
        if (enrollments == null)
        {
            return (new List<Enrollment>(), new List<Enrollment>(), new List<Enrollment>());
        }

        var performingMembers = enrollments
            .Where(e => e.User != null && e.WillAttend && e.EffectiveCategory() != MemberCategory.Leitao)
            .ToList();

        var leitaoMembers = enrollments
            .Where(e => e.User != null && e.WillAttend && e.EffectiveCategory() == MemberCategory.Leitao)
            .ToList();

        var notAttendingMembers = enrollments
            .Where(e => e.User != null && !e.WillAttend)
            .ToList();

        // Apply search filter to performing members, leitões, and not attending
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchTerms = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLower())
                .ToArray();

            performingMembers = performingMembers.Where(e =>
            {
                if (e.User == null) return false;

                var searchableText = $"{e.User.FirstName} {e.User.LastName} {e.User.Nickname} {e.User.Email}".ToLower();
                return searchTerms.All(term => searchableText.Contains(term));
            }).ToList();

            leitaoMembers = leitaoMembers.Where(e =>
            {
                if (e.User == null) return false;

                var searchableText = $"{e.User.FirstName} {e.User.LastName} {e.User.Nickname} {e.User.Email}".ToLower();
                return searchTerms.All(term => searchableText.Contains(term));
            }).ToList();

            notAttendingMembers = notAttendingMembers.Where(e =>
            {
                if (e.User == null) return false;

                var searchableText = $"{e.User.FirstName} {e.User.LastName} {e.User.Nickname} {e.User.Email}".ToLower();
                return searchTerms.All(term => searchableText.Contains(term));
            }).ToList();
        }

        return (performingMembers, leitaoMembers, notAttendingMembers);
    }
}
