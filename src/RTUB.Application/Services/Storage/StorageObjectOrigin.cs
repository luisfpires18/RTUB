using Microsoft.Extensions.Configuration;

namespace RTUB.Application.Services.Storage;

/// <summary>
/// Who owns the object a stored URL points at.
/// </summary>
public enum StorageObjectOrigin
{
    /// <summary>
    /// The URL is not an absolute http/https URL, or no current public origin is configured.
    /// Destructive operations must refuse it - this is the fail-closed case.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The object lives in this environment's own bucket. Read, write and delete are all allowed.
    /// </summary>
    CurrentEnvironment,

    /// <summary>
    /// The object lives in the production bucket and was inherited by a sanitized snapshot.
    /// Readable through the public URL; never writable or deletable from here.
    /// </summary>
    ProductionReference,

    /// <summary>
    /// Some other origin entirely - a hand-edited row, a third-party host, a stale domain.
    /// Never deletable.
    /// </summary>
    External
}

/// <summary>
/// Decides which bucket a stored absolute URL belongs to, so a non-production environment
/// running on a sanitized copy of the production database cannot issue a destructive operation
/// against an object it does not own.
/// </summary>
/// <remarks>
/// <para>A DEV database cloned from production is full of absolute production R2 URLs. Every
/// delete path in the media services turns such a URL into an object key with
/// <c>ExtractObjectKeyFromUrl</c>, which reads the path and ignores the host entirely - so the
/// key survives the crossing and the delete is then issued against whatever bucket the current
/// environment is pointed at. That is safe only for as long as the two buckets differ, which is
/// a configuration property, not a code one. This class makes it a code one.</para>
///
/// <para>Deliberately not a DI service: <see cref="BaseCloudflareStorageService{TLogger}"/>
/// already receives <see cref="IConfiguration"/>, so building the resolver there changes no
/// constructor and no registration in any of the eleven media services.</para>
///
/// <para>Production is unaffected. Its stored URLs sit under its own
/// <c>Cloudflare:R2:PublicUrl</c>, so they resolve to <see cref="StorageObjectOrigin.CurrentEnvironment"/>
/// and behave exactly as before, and it configures no reference origin at all.</para>
/// </remarks>
public sealed class StorageOriginResolver
{
    /// <summary>Public base URL of the bucket this environment reads and writes.</summary>
    public const string CurrentPublicUrlKey = "Cloudflare:R2:PublicUrl";

    /// <summary>
    /// Public base URL of the production bucket, set only in Development/Staging so a DEV app
    /// can still render media inherited from a production snapshot. Never set in production.
    /// </summary>
    public const string ReferencePublicUrlKey = "Cloudflare:R2:ReferencePublicUrl";

    private readonly string? _currentOrigin;
    private readonly string? _referenceOrigin;

    public StorageOriginResolver(string? currentPublicUrl, string? referencePublicUrl)
    {
        _currentOrigin = NormalizeOrigin(currentPublicUrl);
        _referenceOrigin = NormalizeOrigin(referencePublicUrl);
    }

    public static StorageOriginResolver FromConfiguration(IConfiguration configuration) =>
        new(configuration[CurrentPublicUrlKey], configuration[ReferencePublicUrlKey]);

    /// <summary>Whether a production reference origin is configured at all.</summary>
    public bool HasReferenceOrigin => _referenceOrigin != null;

    /// <summary>The normalized production reference origin, or null. Used by the CSP builder.</summary>
    public string? ReferenceOrigin => _referenceOrigin;

    /// <summary>
    /// Classifies a stored URL. Anything that is not an absolute http/https URL - a bare object
    /// key, a relative path, null - is <see cref="StorageObjectOrigin.Unknown"/>, not "ours".
    /// </summary>
    public StorageObjectOrigin Resolve(string? url)
    {
        var origin = NormalizeOrigin(url);

        if (origin == null)
        {
            return StorageObjectOrigin.Unknown;
        }

        // Checked before the reference origin on purpose: if the two were ever configured to the
        // same value, the environment's own bucket wins and nothing changes for it.
        if (_currentOrigin != null && string.Equals(origin, _currentOrigin, StringComparison.OrdinalIgnoreCase))
        {
            return StorageObjectOrigin.CurrentEnvironment;
        }

        if (_referenceOrigin != null && string.Equals(origin, _referenceOrigin, StringComparison.OrdinalIgnoreCase))
        {
            return StorageObjectOrigin.ProductionReference;
        }

        return StorageObjectOrigin.External;
    }

    /// <summary>
    /// The single invariant every destructive path goes through: only an object in this
    /// environment's own bucket may be deleted or overwritten.
    /// </summary>
    public bool IsDeletable(string? url) => Resolve(url) == StorageObjectOrigin.CurrentEnvironment;

    /// <summary>
    /// <c>scheme://host[:port]</c> for an absolute http/https URL, or null for anything else.
    /// Comparing normalized origins rather than string prefixes means a trailing slash, a
    /// default port or a differing path cannot change the answer.
    /// </summary>
    internal static string? NormalizeOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host))
        {
            return null;
        }

        return uri.IsDefaultPort
            ? $"{uri.Scheme}://{uri.Host}"
            : $"{uri.Scheme}://{uri.Host}:{uri.Port}";
    }
}
