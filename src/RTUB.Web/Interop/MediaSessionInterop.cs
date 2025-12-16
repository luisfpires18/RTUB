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
    /// Sets video metadata for the system media overlay
    /// </summary>
    /// <param name="title">Video title</param>
    /// <param name="subtitle">Video subtitle or context (e.g., event name, song name)</param>
    /// <param name="artworkUrl">Artwork URL (optional, defaults to RTUB logo)</param>
    public ValueTask SetVideoMetadataAsync(
        string title,
        string subtitle = "",
        string artworkUrl = "")
    {
        return _js.InvokeVoidAsync(
            "rtubMediaSession.setVideoMetadata",
            title ?? string.Empty,
            subtitle ?? string.Empty,
            artworkUrl ?? string.Empty);
    }

    /// <summary>
    /// Attaches metadata to a video element that will be set when the video plays
    /// </summary>
    /// <param name="videoSelector">CSS selector for the video element (e.g., ".modal-video")</param>
    /// <param name="title">Video title</param>
    /// <param name="subtitle">Video subtitle or context</param>
    /// <param name="artworkUrl">Artwork URL (optional)</param>
    public ValueTask AttachToVideoAsync(
        string videoSelector,
        string title,
        string subtitle = "",
        string artworkUrl = "")
    {
        return _js.InvokeVoidAsync(
            "rtubMediaSession.attachToVideo",
            videoSelector ?? string.Empty,
            title ?? string.Empty,
            subtitle ?? string.Empty,
            artworkUrl ?? string.Empty);
    }

    /// <summary>
    /// Clears the media session metadata
    /// </summary>
    public ValueTask ClearMetadataAsync()
    {
        return _js.InvokeVoidAsync("rtubMediaSession.clearMetadata");
    }
}
