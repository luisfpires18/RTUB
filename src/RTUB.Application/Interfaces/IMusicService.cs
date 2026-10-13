using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React Music area's single entry point (React track 006, docs/react-music.md). Every method
/// takes the caller and applies visibility and <see cref="Helpers.MusicAuthorization"/> itself, so
/// the HTTP endpoints stay thin and nothing trusts the browser.
/// </summary>
public interface IMusicService
{
    Task<MusicAlbumListDto> GetAlbumsAsync(ClaimsPrincipal user);

    Task<MusicResult<MusicAlbumDetailDto>> GetAlbumAsync(int albumId, ClaimsPrincipal user);

    Task<MusicResult<MusicLyricsDto>> GetLyricsAsync(int songId, ClaimsPrincipal user);

    /// <summary>A short-lived audio link without counting a play (the player preloads neighbours).</summary>
    Task<MusicResult<string>> GetAudioUrlAsync(int songId, ClaimsPrincipal user);

    /// <summary>
    /// Counts a play unless <paramref name="listenerKey"/> played this song within its cooldown
    /// (the song's duration, 30 s when unknown), and returns the audio link either way.
    /// </summary>
    Task<MusicResult<MusicPlayDto>> PlayAsync(int songId, ClaimsPrincipal user, string listenerKey);

    Task<MusicResult<IReadOnlyList<MusicVideoDto>>> GetVideosAsync(int songId, ClaimsPrincipal user);

    /// <summary>Audit-logs a video being watched (as the retired page did), with the play cooldown.</summary>
    Task<MusicResult<bool>> RecordVideoPlayAsync(int videoId, ClaimsPrincipal user);

    Task<MusicResult<MusicStatisticsDto>> GetStatisticsAsync(ClaimsPrincipal user);

    Task<MusicResult<MusicAlbumEditDto>> GetAlbumForEditAsync(int albumId, ClaimsPrincipal user);

    /// <summary>Members that can be put on an exclusive album's access list (Owner only).</summary>
    Task<MusicResult<IReadOnlyList<MusicMemberDto>>> GetMembersAsync(ClaimsPrincipal user);

    Task<MusicResult<MusicAlbumSummaryDto>> CreateAlbumAsync(MusicAlbumInput input, MusicCoverUpload? cover, ClaimsPrincipal user);

    Task<MusicResult<MusicAlbumSummaryDto>> UpdateAlbumAsync(int albumId, MusicAlbumInput input, MusicCoverUpload? cover, ClaimsPrincipal user);

    Task<MusicResult<bool>> DeleteAlbumAsync(int albumId, ClaimsPrincipal user);

    Task<MusicResult<MusicSongEditDto>> GetSongForEditAsync(int songId, ClaimsPrincipal user);

    Task<MusicResult<int>> CreateSongAsync(int albumId, MusicSongInput input, ClaimsPrincipal user);

    Task<MusicResult<int>> UpdateSongAsync(int songId, MusicSongInput input, ClaimsPrincipal user);

    Task<MusicResult<bool>> DeleteSongAsync(int songId, ClaimsPrincipal user);

    Task<MusicResult<MusicVideoDto>> UploadVideoAsync(int songId, MusicVideoUpload upload, ClaimsPrincipal user);

    Task<MusicResult<bool>> DeleteVideoAsync(int videoId, ClaimsPrincipal user);
}
