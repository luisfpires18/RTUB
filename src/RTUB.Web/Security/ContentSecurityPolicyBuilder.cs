using System.Text;

namespace RTUB.Security;

/// <summary>
/// Builds RTUB's enforced Content-Security-Policy header value (unit 025).
///
/// Every source in the policy was derived from what the browser actually loads; nothing is
/// whitelisted "just in case", and no directive gets an origin it does not use. See STATE.md
/// for the per-directive evidence.
///
/// One part of the policy cannot be a constant: <b>Cloudflare R2</b> has two distinct origins, both runtime
/// configuration:
/// <list type="bullet">
/// <item><description><c>Cloudflare:R2:PublicUrl</c> - the public bucket (images and video are
/// stored as absolute URLs under it), parsed as an absolute http/https URI so only its
/// normalized <c>scheme://host[:port]</c> ever reaches the header.</description></item>
/// <item><description><c>Cloudflare:R2:AccountId</c> - the S3 API endpoint
/// <c>https://{accountId}.r2.cloudflarestorage.com</c>, which is where the pre-signed URLs used
/// by the audio player and the two PDF iframes point. The id is validated as a single DNS label
/// before it is interpolated.</description></item>
/// </list>
/// <para>Misconfigured or missing values contribute nothing: the source is dropped rather than
/// replaced with a wildcard, so bad configuration can only make R2 content unavailable, never
/// weaken the policy or inject header text.</para>
///
/// <para><c>connect-src</c> is <c>'self'</c> only. Until task 037 it also named the request's own ws:/wss: origin for
/// the Blazor Server circuit (<c>/_blazor</c>); the Blazor host is gone and nothing opens a WebSocket.</para>
/// </summary>
public sealed class ContentSecurityPolicyBuilder
{
    private const string R2EndpointSuffix = ".r2.cloudflarestorage.com";

    /// <summary>The whole policy, built once: nothing in it depends on the request.</summary>
    private readonly string _policy;

    public ContentSecurityPolicyBuilder(IConfiguration configuration)
        : this(configuration["Cloudflare:R2:PublicUrl"],
               configuration["Cloudflare:R2:AccountId"],
               configuration["Cloudflare:R2:ReferencePublicUrl"])
    {
    }

    public ContentSecurityPolicyBuilder(string? r2PublicUrl, string? r2AccountId)
        : this(r2PublicUrl, r2AccountId, null)
    {
    }

    public ContentSecurityPolicyBuilder(string? r2PublicUrl, string? r2AccountId, string? r2ReferencePublicUrl)
    {
        var publicOrigin = NormalizeOrigin(r2PublicUrl);
        var endpointOrigin = NormalizeAccountEndpoint(r2AccountId);
        var referenceOrigin = NormalizeOrigin(r2ReferencePublicUrl);

        // The reference origin is the production bucket, configured only in Development/Staging so
        // a DEV app running on a sanitized production snapshot can still render the media those
        // rows point at. Production sets no such key, so its policy is byte-identical to before.
        // Dropped when it duplicates the environment's own origin, so the header never repeats it.
        if (referenceOrigin != null &&
            string.Equals(referenceOrigin, publicOrigin, StringComparison.OrdinalIgnoreCase))
        {
            referenceOrigin = null;
        }

        _policy = BuildStaticDirectives(publicOrigin, endpointOrigin, referenceOrigin) + "connect-src 'self'";
    }

    /// <summary>The header value.</summary>
    public string Build() => _policy;

    /// <summary>
    /// <c>scheme://host[:port]</c> for an absolute http/https URI, or null for anything else.
    /// Everything after the authority - path, query, fragment, credentials - is discarded, so no
    /// part of the configured string can smuggle a <c>;</c> or a second directive into the header.
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

    /// <summary>
    /// The R2 S3 API origin for an account id. The id must be a single DNS label, which is what
    /// Cloudflare issues, and is what keeps arbitrary configuration text out of the header.
    /// </summary>
    internal static string? NormalizeAccountEndpoint(string? accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return null;
        }

        var id = accountId.Trim();

        return IsDnsLabel(id) ? $"https://{id}{R2EndpointSuffix}" : null;
    }

    private static string BuildStaticDirectives(string? r2PublicOrigin, string? r2EndpointOrigin, string? r2ReferenceOrigin)
    {
        var policy = new StringBuilder();

        void Directive(string name, params string?[] sources)
        {
            policy.Append(name);

            foreach (var source in sources)
            {
                if (!string.IsNullOrEmpty(source))
                {
                    policy.Append(' ').Append(source);
                }
            }

            policy.Append("; ");
        }

        // Fallback for anything not named below.
        Directive("default-src", "'self'");

        // <base href="/"> is the only base element; pin it so injected markup cannot re-root
        // every relative URL on the page.
        Directive("base-uri", "'self'");

        // No <object>/<embed>/<applet> anywhere in the app.
        Directive("object-src", "'none'");

        // RTUB never embeds itself. Mirrors the X-Frame-Options: DENY set alongside it.
        Directive("frame-ancestors", "'none'");

        // The only HTML form posts are /auth/login and /auth/logout.
        Directive("form-action", "'self'");

        // cdnjs -> cropper.min.js, unpkg -> leaflet.js (SRI pinned) until task 032: nothing loads from either now (the
        // React Cropper is canvas-only and Leaflet is bundled with the React map). Dropping them is a follow-up.
        // No 'unsafe-eval': unit 022 removed every JSRuntime eval dispatch, and no component
        // uses an InteractiveWebAssembly/Auto render mode, so no wasm source is needed either.
        Directive("script-src", "'self'", "https://cdnjs.cloudflare.com", "https://unpkg.com");

        // Inline event handler attributes: removed by unit 023 and, for JS-built markup, 025.
        Directive("script-src-attr", "'none'");

        // cdnjs -> cropper.min.css, unpkg -> leaflet.css (SRI pinned) until task 032; unused since, as above.
        Directive("style-src", "'self'", "https://cdnjs.cloudflare.com", "https://unpkg.com");

        // Inline style attributes: removed by unit 024. Dynamic values go through the CSSOM,
        // which CSP does not govern.
        Directive("style-src-attr", "'none'");

        // data: - the cropper and the gallery upload preview render the picked file as a
        //         data: URL before it is uploaded.
        // OSM tiles - the Leaflet basemap of the React members map (MembersMap.tsx; OpenStreetMap since 040, CARTO
        //             before, which started to require an API key).
        // R2 public - avatars, gallery images and event media, stored as absolute URLs.
        // r2ReferenceOrigin - inherited production media on a DEV snapshot. One exact origin, never
        // a wildcard, and absent unless explicitly configured (i.e. never in production).
        Directive("img-src", "'self'", "data:", "https://tile.openstreetmap.org", r2PublicOrigin, r2ReferenceOrigin);

        // Public origin for <video>; the S3 endpoint for the pre-signed album audio URLs.
        Directive("media-src", "'self'", r2PublicOrigin, r2EndpointOrigin, r2ReferenceOrigin);

        // bootstrap-icons ships its woff2 next to its stylesheet; no web font service is used.
        Directive("font-src", "'self'");

        Directive("manifest-src", "'self'");

        // /service-worker.js. Note the worker never receives this header itself - see Program.cs.
        Directive("worker-src", "'self'");

        // The two PDF viewers (Songs lyrics, Roles RGI) frame pre-signed S3 endpoint URLs, and
        // nothing else is framed. With no configured account the app frames nothing at all.
        Directive("frame-src", r2EndpointOrigin ?? "'none'");

        return policy.ToString();
    }

    /// <summary>A single DNS label: letters, digits and interior hyphens, 1-63 characters.</summary>
    private static bool IsDnsLabel(string value)
    {
        if (value.Length is 0 or > 63 || value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-')
            {
                return false;
            }
        }

        return true;
    }
}
