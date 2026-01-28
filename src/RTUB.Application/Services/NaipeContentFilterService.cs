using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for filtering naipe content
/// Extracted from Naipes.razor to improve separation of concerns
/// </summary>
public class NaipeContentFilterService : INaipeContentFilterService
{
    /// <summary>
    /// Filters content by instrument type and search term
    /// </summary>
    /// <param name="allContent">All available content</param>
    /// <param name="selectedInstrument">Selected instrument type (null if none selected)</param>
    /// <param name="searchTerm">Search term to filter by title or description</param>
    /// <returns>Filtered and sorted content list</returns>
    public List<NaipeContentDto> FilterContent(
        IEnumerable<NaipeContentDto> allContent,
        InstrumentType? selectedInstrument,
        string searchTerm)
    {
        // First, get content for the selected instrument (no search filter)
        List<NaipeContentDto> contentForInstrument;
        if (selectedInstrument.HasValue)
        {
            contentForInstrument = allContent.Where(c => c.InstrumentType == selectedInstrument.Value).ToList();
        }
        else
        {
            contentForInstrument = new List<NaipeContentDto>();
        }

        // Apply search filter
        var filteredContent = contentForInstrument;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.ToLower();
            filteredContent = filteredContent.Where(c =>
                c.Title.ToLower().Contains(search) ||
                (c.Description?.ToLower().Contains(search) ?? false)
            ).ToList();
        }

        // Sort by SortOrder
        return filteredContent.OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt).ToList();
    }
}
