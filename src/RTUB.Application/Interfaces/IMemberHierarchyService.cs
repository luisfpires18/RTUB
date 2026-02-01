using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for building member hierarchy (mentor-afilhado relationships)
/// Extracted from Hierarchy.razor to improve separation of concerns
/// </summary>
public interface IMemberHierarchyService
{
    /// <summary>
    /// Builds the hierarchy structure from a collection of members
    /// </summary>
    /// <param name="members">Collection of all members</param>
    /// <returns>Tuple containing (rootMembers, mentorToAfilhados mapping)</returns>
    (List<ApplicationUser> rootMembers, Dictionary<string, List<ApplicationUser>> mentorToAfilhados)
        BuildHierarchy(IEnumerable<ApplicationUser> members);
}
