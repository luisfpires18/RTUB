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
    /// Initialize PWA Media Session module with audio element
    /// </summary>
    /// <param name="audioElementId">ID of the audio element</param>
    /// <param name="dotNetHelper">Optional DotNetObjectReference for callbacks to Blazor</param>
    /// <returns>True if successfully initialized in PWA mode</returns>
    public ValueTask<bool> InitPwaMediaSessionAsync(string audioElementId, DotNetObjectReference<object>? dotNetHelper = null)
    {
        if (dotNetHelper != null)
        {
            return _js.InvokeAsync<bool>("pwaMediaSession.init", audioElementId, dotNetHelper);
        }
        return _js.InvokeAsync<bool>("pwaMediaSession.init", audioElementId);
    }

    /// <summary>
    /// Set the playback queue for PWA mode
    /// </summary>
    /// <param name="queue">Array of track objects</param>
    /// <param name="currentIndex">Index of current track</param>
    public ValueTask SetQueueAsync(object queue, int currentIndex)
    {
        return _js.InvokeVoidAsync("pwaMediaSession.setQueue", queue, currentIndex);
    }

    /// <summary>
    /// Set now playing metadata for PWA mode (legacy, maintained for compatibility)
    /// </summary>
    /// <param name="metadata">Track metadata object</param>
    public ValueTask SetNowPlayingMetadataForPwaAsync(object metadata)
    {
        return _js.InvokeVoidAsync("pwaMediaSession.setNowPlaying", metadata);
    }

    /// <summary>
    /// Update current index when track changes
    /// </summary>
    /// <param name="newIndex">New current index</param>
    public ValueTask UpdateCurrentIndexAsync(int newIndex)
    {
        return _js.InvokeVoidAsync("pwaMediaSession.updateCurrentIndex", newIndex);
    }

    /// <summary>
    /// Check if app is running in PWA mode
    /// </summary>
    /// <returns>True if in PWA mode</returns>
    public ValueTask<bool> IsPwaModeAsync()
    {
        return _js.InvokeAsync<bool>("pwaMediaSession.detectPwaMode");
    }

    /// <summary>
    /// Cleanup PWA Media Session handlers
    /// </summary>
    public ValueTask CleanupPwaMediaSessionAsync()
    {
        return _js.InvokeVoidAsync("pwaMediaSession.cleanup");
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
