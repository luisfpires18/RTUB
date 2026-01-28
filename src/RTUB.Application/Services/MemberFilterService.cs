using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for filtering members with complex logic
/// Extracted from Members.razor to improve separation of concerns
/// </summary>
public class MemberFilterService : IMemberFilterService
{
    /// <summary>
    /// Filters members by search term, active status, instrument, and category
    /// Handles complex category filtering including Tuno subcategories (Veterano, Tunossauro, Fundador)
    /// </summary>
    /// <param name="users">Collection of users to filter</param>
    /// <param name="searchTerm">Search term to filter by name, nickname, email, phone, or city</param>
    /// <param name="showActiveOnly">If true, only show non-retired members</param>
    /// <param name="selectedInstrumentFilter">Selected instrument type filter (empty string if none)</param>
    /// <param name="selectedCategoryFilter">Selected category filter (empty string if none)</param>
    /// <param name="selectedSubCategoryFilter">Selected subcategory filter for Tuno category (empty string if none)</param>
    /// <param name="userAllInstruments">Dictionary mapping userId to their list of instruments</param>
    /// <returns>Tuple containing (filteredRegularMembers, filteredLeitoes, allFilteredUsers)</returns>
    public (List<ApplicationUser> filteredRegularMembers, List<ApplicationUser> filteredLeitoes, List<ApplicationUser> allFilteredUsers) 
        FilterMembers(
            IEnumerable<ApplicationUser> users,
            string searchTerm,
            bool showActiveOnly,
            string selectedInstrumentFilter,
            string selectedCategoryFilter,
            string selectedSubCategoryFilter,
            Dictionary<string, List<MemberInstrument>> userAllInstruments)
    {
        if (users == null) 
        {
            return (new List<ApplicationUser>(), new List<ApplicationUser>(), new List<ApplicationUser>());
        }

        // Use SearchHelper for filtering
        var searchHelper = new SearchHelper<ApplicationUser> { SearchTerm = searchTerm };
        var allFilteredUsers = searchHelper.FilterMultiple(users.ToList(), new List<Func<ApplicationUser, string>>
        {
            u => u.FirstName ?? "",
            u => u.LastName ?? "",
            u => u.Nickname ?? "",
            u => u.Email ?? "",
            u => u.PhoneNumber ?? "",
            u => u.City ?? ""
        });

        // Filter by IsRetired status
        // When showActiveOnly is ON (true), show only active members (IsRetired = false)
        // When showActiveOnly is OFF (false), show all members
        if (showActiveOnly)
        {
            allFilteredUsers = allFilteredUsers
                .Where(u => !u.IsRetired)
                .ToList();
        }

        // Apply instrument filter to all users before separating (applies to both grids)
        // Now checks both primary and secondary instruments
        if (!string.IsNullOrEmpty(selectedInstrumentFilter))
        {
            var instrumentEnum = Enum.Parse<InstrumentType>(selectedInstrumentFilter);
            allFilteredUsers = allFilteredUsers
                .Where(u => userAllInstruments.ContainsKey(u.Id) && 
                           userAllInstruments[u.Id].Any(i => i.InstrumentType == instrumentEnum))
                .ToList();
        }
        
        // Apply category filter to all users before separating (affects which grids are shown)
        // Now supports VETERANO, TUNOSSAURO, and TUNO HONORARIO subcategories
        // TunoHonorario members ONLY appear in TunoHonorario filter (special case)
        if (!string.IsNullOrEmpty(selectedCategoryFilter))
        {
            var categoryEnum = Enum.Parse<MemberCategory>(selectedCategoryFilter);
            
            // Special handling for TunoHonorario: they are exclusive and don't appear in other filters
            if (categoryEnum == MemberCategory.TunoHonorario)
            {
                // Only show users with explicit TunoHonorario category
                allFilteredUsers = allFilteredUsers
                    .Where(u => u.Categories.Contains(MemberCategory.TunoHonorario))
                    .ToList();
            }
            // When Tuno is selected as category, apply subcategory filter if set
            else if (categoryEnum == MemberCategory.Tuno)
            {
                // If subcategory filter is set, apply it
                if (!string.IsNullOrEmpty(selectedSubCategoryFilter))
                {
                    var subCategoryEnum = Enum.Parse<MemberCategory>(selectedSubCategoryFilter);
                    
                    if (subCategoryEnum == MemberCategory.Tuno)
                    {
                        // Show only base Tuno (not Veterano, not Tunossauro, not Fundador)
                        allFilteredUsers = allFilteredUsers
                            .Where(u => !u.Categories.Contains(MemberCategory.TunoHonorario) &&
                                       u.Categories.Contains(MemberCategory.Tuno) &&
                                       !u.QualifiesForVeterano() &&
                                       !u.QualifiesForTunossauro() &&
                                       !u.Categories.Contains(MemberCategory.Fundador))
                            .ToList();
                    }
                    else if (subCategoryEnum == MemberCategory.Veterano)
                    {
                        // Show Veterano
                        allFilteredUsers = allFilteredUsers
                            .Where(u => !u.Categories.Contains(MemberCategory.TunoHonorario) &&
                                       u.QualifiesForVeterano() &&
                                       !u.QualifiesForTunossauro())
                            .ToList();
                    }
                    else if (subCategoryEnum == MemberCategory.Tunossauro)
                    {
                        // Show Tunossauro
                        allFilteredUsers = allFilteredUsers
                            .Where(u => !u.Categories.Contains(MemberCategory.TunoHonorario) &&
                                       u.QualifiesForTunossauro())
                            .ToList();
                    }
                    else if (subCategoryEnum == MemberCategory.Fundador)
                    {
                        // Show Fundador
                        allFilteredUsers = allFilteredUsers
                            .Where(u => u.Categories.Contains(MemberCategory.Fundador))
                            .ToList();
                    }
                }
                else
                {
                    // No subcategory filter - show all Tuno (including Veterano, Tunossauro, Fundador)
                    allFilteredUsers = allFilteredUsers
                        .Where(u => !u.Categories.Contains(MemberCategory.TunoHonorario) &&
                                   (u.Categories.Contains(MemberCategory.Tuno) ||
                                    u.QualifiesForVeterano() ||
                                    u.QualifiesForTunossauro()))
                        .ToList();
                }
            }
            else
            {
                // For other categories (Leitao, Caloiro), exclude TunoHonorario members
                allFilteredUsers = allFilteredUsers
                    .Where(u => !u.Categories.Contains(MemberCategory.TunoHonorario) &&
                               u.Categories.Contains(categoryEnum))
                    .ToList();
            }
        }
        else if (string.IsNullOrWhiteSpace(searchTerm))
        {
            // When no filter or search is selected, exclude TunoHonorario from general view
            // (they only appear when specifically filtering by TunoHonorario or via search)
            allFilteredUsers = allFilteredUsers
                .Where(u => !u.Categories.Contains(MemberCategory.TunoHonorario))
                .ToList();
        }
        
        // Separate Leitões from regular members
        var filteredLeitoes = allFilteredUsers.Where(u => u.IsLeitao()).ToList();
        var filteredRegularMembers = allFilteredUsers.Where(u => !u.IsLeitao()).ToList();
        
        return (filteredRegularMembers, filteredLeitoes, allFilteredUsers);
    }
}
