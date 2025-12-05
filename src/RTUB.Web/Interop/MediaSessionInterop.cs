using Microsoft.JSInterop;

namespace RTUB.Web.Interop;

/// <summary>
/// JavaScript interop for Media Session API to control lock screen / system media overlay
/// </summary>
public class MediaSessionInterop
{
    private readonly IJSRuntime _js;

    public MediaSessionInterop(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// Sets the now playing metadata for the system media overlay
    /// </summary>
    /// <param name="title">Song title</param>
    /// <param name="album">Album name</param>
    /// <param name="artworkUrl">Album cover URL (optional)</param>
    /// <param name="fallbackArtworkUrl">Fallback artwork URL (e.g., RTUB logo)</param>
    public ValueTask SetNowPlayingMetadataAsync(
        string title,
        string album,
        string artworkUrl,
        string fallbackArtworkUrl)
    {
        return _js.InvokeVoidAsync(
            "rtubMediaSession.setNowPlayingMetadata",
            title ?? string.Empty,
            album ?? string.Empty,
            artworkUrl ?? string.Empty,
            fallbackArtworkUrl ?? string.Empty);
    }

    /// <summary>
    /// Clears the media session metadata
    /// </summary>
    public ValueTask ClearMetadataAsync()
    {
        return _js.InvokeVoidAsync("rtubMediaSession.clearMetadata");
    }
}
