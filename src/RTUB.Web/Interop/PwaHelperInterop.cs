using Microsoft.JSInterop;

namespace RTUB.Web.Interop;

/// <summary>
/// JavaScript interop for PWA helper utilities
/// </summary>
public class PwaHelperInterop
{
    private readonly IJSRuntime _jsRuntime;

    public PwaHelperInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Checks if the app is running in PWA/standalone mode
    /// </summary>
    /// <returns>True if running as installed PWA, false otherwise</returns>
    public async Task<bool> IsPwaModeAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("pwaHelper.isPwaMode");
        }
        catch
        {
            // If JavaScript interop fails, assume not PWA mode
            return false;
        }
    }

    /// <summary>
    /// Returns the Android client mode: "TWA", "PWA", or "Browser".
    /// Returns null if not an Android device.
    /// </summary>
    public async Task<string?> GetAndroidClientModeAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("pwaHelper.getAndroidClientMode");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if the app should use mobile behavior (PWA or mobile browser)
    /// </summary>
    /// <returns>True if running as PWA or on mobile browser, false for desktop web</returns>
    public async Task<bool> IsMobilePwaOrBrowserAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("pwaHelper.isMobilePwaOrBrowser");
        }
        catch
        {
            // If JavaScript interop fails, fallback to false (desktop behavior)
            return false;
        }
    }
}
