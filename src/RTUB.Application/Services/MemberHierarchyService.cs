using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for building member hierarchy (mentor-afilhado relationships)
/// Extracted from Hierarchy.razor to improve separation of concerns
/// </summary>
public class MemberHierarchyService : IMemberHierarchyService
{
    /// <summary>
    /// Builds the hierarchy structure from a collection of members
    /// </summary>
    /// <param name="members">Collection of all members</param>
    /// <returns>Tuple containing (rootMembers, mentorToAfilhados mapping)</returns>
    public (List<ApplicationUser> rootMembers, Dictionary<string, List<ApplicationUser>> mentorToAfilhados) 
        BuildHierarchy(IEnumerable<ApplicationUser> members)
    {
        // Load all members excluding Leitões (only show Caloiro, Tuno, Veterano, Tunossauro)
        // Must load to memory first before using IsEffectiveMember() extension method
        var effectiveMembers = members
            .Where(u => u.IsEffectiveMember())
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToList();

        // Create dictionary for O(1) mentor lookups instead of O(n) FirstOrDefault calls
        var membersById = effectiveMembers.ToDictionary(m => m.Id);

        // Build mentor-to-afilhados mapping
        // Only include mentors who are Tunos (not Caloiros)
        var mentorToAfilhados = new Dictionary<string, List<ApplicationUser>>();
        foreach (var member in effectiveMembers)
        {
            if (!string.IsNullOrEmpty(member.MentorId))
            {
                // Get the mentor using dictionary lookup (O(1) instead of O(n))
                membersById.TryGetValue(member.MentorId, out var mentor);
                
                // Only add if mentor is Tuno, Veterano, or Tunossauro (not Caloiro)
                if (mentor != null && mentor.IsTunoOrHigher())
                {
                    if (!mentorToAfilhados.ContainsKey(member.MentorId))
                    {
                        mentorToAfilhados[member.MentorId] = new List<ApplicationUser>();
                    }
                    mentorToAfilhados[member.MentorId].Add(member);
                }
            }
        }

        // Find root members (those without mentors or whose mentor is not in the displayed list or not a valid Tuno mentor)
        var rootMembers = effectiveMembers
            .Where(m => 
            {
                if (string.IsNullOrEmpty(m.MentorId))
                    return true;
                    
                // Use dictionary lookup (O(1) instead of O(n))
                membersById.TryGetValue(m.MentorId, out var mentor);
                
                // Include as root if mentor doesn't exist OR mentor is not a Tuno/Veterano/Tunossauro
                return mentor == null || !mentor.IsTunoOrHigher();
            })
            .OrderBy(m => m.FirstName)
            .ThenBy(m => m.LastName)
            .ToList();

        return (rootMembers, mentorToAfilhados);
    }
}
