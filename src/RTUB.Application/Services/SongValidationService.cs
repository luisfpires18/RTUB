using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for validating song-related files
/// Extracted from Songs.razor to improve separation of concerns
/// </summary>
public class SongValidationService : ISongValidationService
{
    private readonly ILogger<SongValidationService> _logger;

    public SongValidationService(ILogger<SongValidationService> logger)
    {
        _logger = logger;
    }

    public bool ValidateVideoFileSize(IBrowserFile file, long maxFileSizeBytes = 100 * 1024 * 1024)
    {
        if (file.Size > maxFileSizeBytes)
        {
            _logger.LogWarning("Video file size ({FileSize} bytes) exceeds maximum allowed size of {MaxSize} bytes", file.Size, maxFileSizeBytes);
            return false;
        }
        return true;
    }
}
