using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for processing song content (video titles, etc.)
/// Extracted from Songs.razor to improve separation of concerns
/// </summary>
public class SongContentService : ISongContentService
{
    private const int MaxCleanTitleLength = 50;
    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".avi", ".mkv", ".webm", ".flv" };

    public string GetCleanVideoTitle(SongVideo video, Song song, int videoNumber)
    {
        if (song == null)
            return $"Vídeo {videoNumber}";

        // If no title exists, generate generic one
        if (string.IsNullOrWhiteSpace(video.Title))
        {
            return $"{song.Title} - Vídeo {videoNumber}";
        }

        // Check if title looks like a storage key (long, contains file extensions, or path separators)
        var title = video.Title.Trim();
        var looksLikeKey = title.Length > MaxCleanTitleLength ||
                          VideoExtensions.Any(ext => title.Contains(ext, StringComparison.OrdinalIgnoreCase)) ||
                          title.Contains("/") ||
                          title.Contains("\\");

        if (looksLikeKey)
        {
            return $"{song.Title} - Vídeo {videoNumber}";
        }

        // Title looks good, use as-is
        return title;
    }
}
