using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Gallery uploads, edits, deletes and person tags behind the React /gallery (React track 015). Every rule is
/// decided here from the caller. Writes go through <see cref="IGalleryMediaService"/> and
/// <see cref="IGalleryMediaStorageService"/>; the tag push goes through <see cref="IPushNotificationService"/>.
/// </summary>
public interface IGalleryManagementService
{
    /// <summary>Members to tag (not expelled), by nickname or name.</summary>
    Task<EventResult<IReadOnlyList<GalleryTaggableDto>>> SearchPeopleAsync(string? query, ClaimsPrincipal user);

    Task<EventResult<GalleryEditDto>> GetForEditAsync(int id, ClaimsPrincipal user);

    /// <summary>Stores the file, saves the item and its tags, then pushes to the people tagged (as before).</summary>
    Task<EventResult<GalleryItemDto>> UploadAsync(GalleryUpload upload, ClaimsPrincipal user);

    /// <summary>Title, date, members-only and tags; no notification (as before).</summary>
    Task<EventResult<GalleryItemDto>> UpdateAsync(int id, GalleryEditInput input, ClaimsPrincipal user);

    /// <summary>Deletes the stored file, then the item (its tags go with it).</summary>
    Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user);
}
