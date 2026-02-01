using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for filtering members with complex logic
/// Extracted from Members.razor to improve separation of concerns
/// </summary>
public interface IMemberFilterService
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
    (List<ApplicationUser> filteredRegularMembers, List<ApplicationUser> filteredLeitoes, List<ApplicationUser> allFilteredUsers)
        FilterMembers(
            IEnumerable<ApplicationUser> users,
            string searchTerm,
            bool showActiveOnly,
            string selectedInstrumentFilter,
            string selectedCategoryFilter,
            string selectedSubCategoryFilter,
            Dictionary<string, List<MemberInstrument>> userAllInstruments);
}
