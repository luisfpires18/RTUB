using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Read side of the React /gallery (React track 009). Visibility is decided here from the caller:
/// visitors get public items only, signed-in members also get members-only items and person tags.
/// </summary>
public interface IGalleryTimelineService
{
    Task<GalleryTimelineDto> GetTimelineAsync(ClaimsPrincipal user, GalleryQuery query);

    /// <summary>One item, or null when it does not exist or the caller may not see it.</summary>
    Task<GalleryItemDto?> GetItemAsync(int id, ClaimsPrincipal user);
}
