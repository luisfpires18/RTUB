using RTUB.Application.DTOs;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for filtering naipe content
/// Extracted from Naipes.razor to improve separation of concerns
/// </summary>
public interface INaipeContentFilterService
{
    /// <summary>
    /// Filters content by instrument type and search term
    /// </summary>
    /// <param name="allContent">All available content</param>
    /// <param name="selectedInstrument">Selected instrument type (null if none selected)</param>
    /// <param name="searchTerm">Search term to filter by title or description</param>
    /// <returns>Filtered and sorted content list</returns>
    List<NaipeContentDto> FilterContent(
        IEnumerable<NaipeContentDto> allContent,
        InstrumentType? selectedInstrument,
        string searchTerm);
}
