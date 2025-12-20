namespace RTUB.Application.Utilities;

/// <summary>
/// Helper class for determining MIME types from file extensions
/// Provides fallback for mobile uploads where ContentType may be missing or incorrect
/// </summary>
public static class MimeTypeHelper
{
    private static readonly Dictionary<string, string> VideoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".mp4", "video/mp4" },
        { ".mov", "video/quicktime" },
        { ".avi", "video/x-msvideo" },
        { ".wmv", "video/x-ms-wmv" },
        { ".flv", "video/x-flv" },
        { ".webm", "video/webm" },
        { ".mkv", "video/x-matroska" },
        { ".m4v", "video/x-m4v" },
        { ".3gp", "video/3gpp" },
        { ".3g2", "video/3gpp2" },
        { ".mpg", "video/mpeg" },
        { ".mpeg", "video/mpeg" }
    };

    private static readonly Dictionary<string, string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".gif", "image/gif" },
        { ".bmp", "image/bmp" },
        { ".webp", "image/webp" },
        { ".svg", "image/svg+xml" },
        { ".ico", "image/x-icon" },
        { ".tiff", "image/tiff" },
        { ".tif", "image/tiff" }
    };

    /// <summary>
    /// Gets the MIME type for a video file, with fallback to file extension
    /// </summary>
    /// <param name="fileName">The name of the file</param>
    /// <param name="providedContentType">The content type provided by the browser (may be empty or incorrect on mobile)</param>
    /// <returns>A valid MIME type for the video</returns>
    public static string GetVideoMimeType(string fileName, string? providedContentType)
    {
        // If provided content type is valid and looks like a video type, use it
        if (!string.IsNullOrWhiteSpace(providedContentType) && 
            providedContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return providedContentType;
        }

        // Fallback to file extension
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(extension) && VideoMimeTypes.TryGetValue(extension, out var mimeType))
        {
            return mimeType;
        }

        // Default fallback for unknown video types (most common format)
        return "video/mp4";
    }

    /// <summary>
    /// Gets the MIME type for an image file, with fallback to file extension
    /// </summary>
    /// <param name="fileName">The name of the file</param>
    /// <param name="providedContentType">The content type provided by the browser (may be empty or incorrect on mobile)</param>
    /// <returns>A valid MIME type for the image</returns>
    public static string GetImageMimeType(string fileName, string? providedContentType)
    {
        // If provided content type is valid and looks like an image type, use it
        if (!string.IsNullOrWhiteSpace(providedContentType) && 
            providedContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return providedContentType;
        }

        // Fallback to file extension
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(extension) && ImageMimeTypes.TryGetValue(extension, out var mimeType))
        {
            return mimeType;
        }

        // Default fallback for unknown image types (most common format)
        return "image/jpeg";
    }

    /// <summary>
    /// Gets the MIME type for any media file (image or video), with fallback to file extension
    /// </summary>
    /// <param name="fileName">The name of the file</param>
    /// <param name="providedContentType">The content type provided by the browser (may be empty or incorrect on mobile)</param>
    /// <param name="isVideo">True if this is a video file, false for image</param>
    /// <returns>A valid MIME type for the media</returns>
    public static string GetMediaMimeType(string fileName, string? providedContentType, bool isVideo)
    {
        return isVideo 
            ? GetVideoMimeType(fileName, providedContentType)
            : GetImageMimeType(fileName, providedContentType);
    }
}
