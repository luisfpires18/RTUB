namespace RTUB.Core.Helpers;

/// <summary>
/// Helper class for converting Cloudflare R2 URLs to proxied URLs for proper caching
/// </summary>
public static class ImageUrlHelper
{
    /// <summary>
    /// Converts a Cloudflare R2 public URL to a proxied URL that supports proper ETag caching
    /// </summary>
    /// <param name="imageUrl">The image URL (can be R2 public URL, proxy URL, or relative path)</param>
    /// <returns>Proxied URL that will serve images with proper caching headers</returns>
    public static string ToProxiedUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        // If already a proxy URL (starts with /api/images/), return as-is
        if (imageUrl.StartsWith("/api/images/", StringComparison.OrdinalIgnoreCase))
        {
            return imageUrl;
        }

        // If it's a relative path starting with /images/, proxy it
        if (imageUrl.StartsWith("/images/", StringComparison.OrdinalIgnoreCase))
        {
            return $"/api{imageUrl}";
        }

        // If it's a full R2 URL (https://pub-xxx.r2.dev/...), extract the object key and proxy it
        if (imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(imageUrl);
                // Extract the path (everything after the domain)
                var objectKey = uri.AbsolutePath.TrimStart('/');
                return $"/api/images/{objectKey}";
            }
            catch
            {
                // If URL parsing fails, return original
                return imageUrl;
            }
        }

        // For any other format, assume it's already correct or fallback to original
        return imageUrl;
    }
}
