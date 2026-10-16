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
            // #223: a member recorded as their own padrinho is shown as a root (the link is not a padrinho relation).
            if (!string.IsNullOrEmpty(member.MentorId) && member.MentorId != member.Id)
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
                if (string.IsNullOrEmpty(m.MentorId) || m.MentorId == m.Id)
                    return true;

                // Use dictionary lookup (O(1) instead of O(n))
                membersById.TryGetValue(m.MentorId, out var mentor);

                // Include as root if mentor doesn't exist OR mentor is not a Tuno/Veterano/Tunossauro
                return mentor == null || !mentor.IsTunoOrHigher();
            })
            .OrderBy(m => m.FirstName)
            .ThenBy(m => m.LastName)
            .ToList();

        BreakLoops(effectiveMembers, rootMembers, mentorToAfilhados);
        return (rootMembers, mentorToAfilhados);
    }

    /// <summary>
    /// #223 defensive guard: members in a padrinho loop (A → B → A, all Tunos) have a valid padrinho each, so none is a
    /// root and the whole loop would silently vanish from the tree. Each loop is cut at its first member by name, who
    /// becomes a root; the rest stay under their padrinhos. Only the returned tree changes - the stored MentorId does
    /// not. Valid data (no loops) is untouched. Real data had none (#223 audit).
    /// </summary>
    private static void BreakLoops(List<ApplicationUser> members, List<ApplicationUser> roots, Dictionary<string, List<ApplicationUser>> afilhados)
    {
        var reached = new HashSet<string>();
        void Reach(ApplicationUser member)
        {
            if (!reached.Add(member.Id))
                return;
            foreach (var child in afilhados.GetValueOrDefault(member.Id) ?? [])
                Reach(child);
        }

        roots.ForEach(Reach);
        var cut = false;
        // members is ordered by name, so each loop is cut at its first member by name.
        foreach (var lost in members.Where(m => !reached.Contains(m.Id)).ToList())
        {
            if (reached.Contains(lost.Id))
                continue;

            afilhados.GetValueOrDefault(lost.MentorId!)?.Remove(lost);
            roots.Add(lost);
            Reach(lost);
            cut = true;
        }

        if (cut)
        {
            // The same order as the roots above (OrderBy FirstName, then LastName).
            var ordered = roots.OrderBy(m => m.FirstName).ThenBy(m => m.LastName).ToList();
            roots.Clear();
            roots.AddRange(ordered);
        }
    }
}
