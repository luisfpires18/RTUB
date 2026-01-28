using Microsoft.AspNetCore.Components.Forms;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for validating song-related files
/// Extracted from Songs.razor to improve separation of concerns
/// </summary>
public interface ISongValidationService
{
    /// <summary>
    /// Validates if a video file size is within allowed limits
    /// </summary>
    /// <param name="file">The file to validate</param>
    /// <param name="maxFileSizeBytes">Maximum allowed file size in bytes (default: 100MB)</param>
    /// <returns>True if file is valid, false if it exceeds size limit</returns>
    bool ValidateVideoFileSize(IBrowserFile file, long maxFileSizeBytes = 100 * 1024 * 1024);
}
