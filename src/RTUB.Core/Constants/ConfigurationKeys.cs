namespace RTUB.Core.Constants;

/// <summary>
/// Configuration keys for application settings
/// Provides centralized access to configuration key strings
/// </summary>
public static class ConfigurationKeys
{
    /// <summary>
    /// Storage configuration keys
    /// </summary>
    public static class Storage
    {
        /// <summary>
        /// URL expiration time in minutes for temporary storage URLs
        /// </summary>
        public const string UrlExpirationMinutes = "Storage:UrlExpirationMinutes";
        
        /// <summary>
        /// HTTP client timeout in seconds for storage operations
        /// </summary>
        public const string HttpClientTimeoutSeconds = "Storage:HttpClientTimeoutSeconds";
    }

    /// <summary>
    /// Cache configuration keys
    /// </summary>
    public static class Cache
    {
        /// <summary>
        /// Absolute expiration time in hours for cached items
        /// </summary>
        public const string AbsoluteExpirationHours = "Cache:AbsoluteExpirationHours";
        
        /// <summary>
        /// Sliding expiration time in minutes for cached items
        /// </summary>
        public const string SlidingExpirationMinutes = "Cache:SlidingExpirationMinutes";
        
        /// <summary>
        /// PDF cache absolute expiration in hours
        /// </summary>
        public const string PdfAbsoluteExpirationHours = "Cache:PdfAbsoluteExpirationHours";
        
        /// <summary>
        /// PDF cache sliding expiration in minutes
        /// </summary>
        public const string PdfSlidingExpirationMinutes = "Cache:PdfSlidingExpirationMinutes";
    }

    /// <summary>
    /// Pagination configuration keys
    /// </summary>
    public static class Pagination
    {
        /// <summary>
        /// Default page size for audit log queries
        /// </summary>
        public const string AuditLogPageSize = "Pagination:AuditLogPageSize";
        
        /// <summary>
        /// Default page size for comment queries
        /// </summary>
        public const string CommentPageSize = "Pagination:CommentPageSize";
        
        /// <summary>
        /// Default page size for post queries
        /// </summary>
        public const string PostPageSize = "Pagination:PostPageSize";
    }
}
