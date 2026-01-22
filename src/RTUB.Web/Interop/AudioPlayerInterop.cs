using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace RTUB.Web.Interop;

/// <summary>
/// JavaScript interop for reliable audio playback
/// </summary>
public class AudioPlayerInterop
{
    private readonly IJSRuntime _js;
    private readonly ILogger<AudioPlayerInterop> _logger;

    public AudioPlayerInterop(IJSRuntime js, ILogger<AudioPlayerInterop> logger)
    {
        _js = js;
        _logger = logger;
    }

    /// <summary>
    /// Play audio element by ID with proper loading and error handling
    /// </summary>
    /// <param name="audioElementId">ID of the audio element</param>
    /// <returns>True if playback started successfully</returns>
    public async ValueTask<bool> PlayAudioAsync(string audioElementId)
    {
        try
        {
            return await _js.InvokeAsync<bool>("rtubAudioPlayer.playAudio", audioElementId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to play audio element: {AudioElementId}", audioElementId);
            return false;
        }
    }

    /// <summary>
    /// Stop audio playback
    /// </summary>
    /// <param name="audioElementId">ID of the audio element</param>
    public async ValueTask StopAudioAsync(string audioElementId)
    {
        try
        {
            await _js.InvokeVoidAsync("rtubAudioPlayer.stopAudio", audioElementId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop audio element: {AudioElementId}", audioElementId);
        }
    }
}
