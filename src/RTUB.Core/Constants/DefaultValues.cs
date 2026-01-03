namespace RTUB.Core.Constants;

/// <summary>
/// Default values for application configuration
/// Provides sensible defaults when configuration is not explicitly set
/// </summary>
public static class DefaultValues
{
    /// <summary>
    /// Default storage configuration values
    /// </summary>
    public static class Storage
    {
        /// <summary>
        /// Default URL expiration time in minutes (1 hour)
        /// </summary>
        public const int UrlExpirationMinutes = 60;

        /// <summary>
        /// Default HTTP client timeout in seconds (30 seconds)
        /// </summary>
        public const int HttpClientTimeoutSeconds = 30;
    }

    /// <summary>
    /// Default cache configuration values
    /// </summary>
    public static class Cache
    {
        /// <summary>
        /// Default absolute expiration time in hours (1 hour)
        /// </summary>
        public const int AbsoluteExpirationHours = 1;

        /// <summary>
        /// Default sliding expiration time in minutes (30 minutes)
        /// </summary>
        public const int SlidingExpirationMinutes = 30;

        /// <summary>
        /// Default PDF cache absolute expiration in hours (1 hour)
        /// </summary>
        public const int PdfAbsoluteExpirationHours = 1;

        /// <summary>
        /// Default PDF cache sliding expiration in minutes (30 minutes)
        /// </summary>
        public const int PdfSlidingExpirationMinutes = 30;

        /// <summary>
        /// Default cache size for eviction policy
        /// </summary>
        public const int DefaultCacheSize = 1;
    }

    /// <summary>
    /// Default pagination values
    /// </summary>
    public static class Pagination
    {
        /// <summary>
        /// Default page size for audit log queries
        /// </summary>
        public const int AuditLogPageSize = 100;

        /// <summary>
        /// Default page size for comment queries
        /// </summary>
        public const int CommentPageSize = 50;

        /// <summary>
        /// Default page size for post queries
        /// </summary>
        public const int PostPageSize = 20;

        /// <summary>
        /// General default page size
        /// </summary>
        public const int DefaultPageSize = 10;
    }

    /// <summary>
    /// Default messaging configuration values
    /// </summary>
    public static class Messaging
    {
        /// <summary>
        /// Maximum length of message preview text before truncation
        /// </summary>
        public const int MessagePreviewMaxLength = 100;
    }

    /// <summary>
    /// Default S3/Storage configuration values
    /// </summary>
    public static class S3
    {
        /// <summary>
        /// Default cache control max-age for immutable assets (1 year in seconds)
        /// </summary>
        public const int ImmutableAssetsCacheMaxAge = 31536000;

        /// <summary>
        /// Cache control header for immutable assets
        /// </summary>
        public const string ImmutableAssetsCacheControl = "public, max-age=31536000, immutable";
    }
}
