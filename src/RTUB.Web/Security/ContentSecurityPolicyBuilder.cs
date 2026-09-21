using System.Text;

namespace RTUB.Security;

/// <summary>
/// Builds RTUB's enforced Content-Security-Policy header value (unit 025).
///
/// Every source in the policy was derived from what the browser actually loads; nothing is
/// whitelisted "just in case", and no directive gets an origin it does not use. See STATE.md
/// for the per-directive evidence.
///
/// Two parts of the policy cannot be constants:
///
/// <para><b>Cloudflare R2</b> has two distinct origins, both runtime configuration:</para>
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
/// <para><b>The WebSocket source</b> is per-request. Blazor Server's circuit runs over
/// <c>/_blazor</c>, and CSP3's <c>'self'</c> is not reliably taken to cover <c>ws:</c>/<c>wss:</c>
/// in every browser, so the exact scheme+host of the current request is emitted instead of the
/// broad <c>ws:</c>/<c>wss:</c> schemes.</para>
/// </summary>
public sealed class ContentSecurityPolicyBuilder
{
    private const string R2EndpointSuffix = ".r2.cloudflarestorage.com";

    /// <summary>Everything before <c>connect-src</c>, built once.</summary>
    private readonly string _staticDirectives;

    public ContentSecurityPolicyBuilder(IConfiguration configuration)
        : this(configuration["Cloudflare:R2:PublicUrl"], configuration["Cloudflare:R2:AccountId"])
    {
    }

    public ContentSecurityPolicyBuilder(string? r2PublicUrl, string? r2AccountId)
    {
        var publicOrigin = NormalizeOrigin(r2PublicUrl);
        var endpointOrigin = NormalizeAccountEndpoint(r2AccountId);

        _staticDirectives = BuildStaticDirectives(publicOrigin, endpointOrigin);
    }

    /// <summary>
    /// Produces the full header value for one request. <paramref name="requestHost"/> is the
    /// Host header, which a client controls, so it is only used when it looks like a host.
    /// </summary>
    public string Build(string? requestScheme, string? requestHost)
    {
        var socketSource = WebSocketSource(requestScheme, requestHost);

        return socketSource is null
            ? _staticDirectives + "connect-src 'self'"
            : _staticDirectives + "connect-src 'self' " + socketSource;
    }

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

    /// <summary>
    /// <c>ws://host[:port]</c> or <c>wss://host[:port]</c> matching the current request, or null
    /// when the scheme is not http/https or the host is not a plain host[:port].
    /// </summary>
    internal static string? WebSocketSource(string? requestScheme, string? requestHost)
    {
        if (string.IsNullOrEmpty(requestHost) || !IsHostWithOptionalPort(requestHost))
        {
            return null;
        }

        return requestScheme switch
        {
            "https" => $"wss://{requestHost}",
            "http" => $"ws://{requestHost}",
            _ => null
        };
    }

    private static string BuildStaticDirectives(string? r2PublicOrigin, string? r2EndpointOrigin)
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

        // cdnjs -> cropper.min.js, unpkg -> leaflet.js (SRI pinned), jsdelivr -> pixi.min.js.
        // No 'unsafe-eval': unit 022 removed every JSRuntime eval dispatch, and no component
        // uses an InteractiveWebAssembly/Auto render mode, so no wasm source is needed either.
        Directive("script-src", "'self'",
            "https://cdnjs.cloudflare.com", "https://unpkg.com", "https://cdn.jsdelivr.net");

        // Inline event handler attributes: removed by unit 023 and, for JS-built markup, 025.
        Directive("script-src-attr", "'none'");

        // cdnjs -> cropper.min.css, unpkg -> leaflet.css (SRI pinned).
        Directive("style-src", "'self'", "https://cdnjs.cloudflare.com", "https://unpkg.com");

        // Inline style attributes: removed by unit 024. Dynamic values go through the CSSOM,
        // which CSP does not govern.
        Directive("style-src-attr", "'none'");

        // data: - the cropper and the gallery upload preview render the picked file as a
        //         data: URL before it is uploaded.
        // carto - the Leaflet dark-matter tiles in memberMap.js.
        // R2 public - avatars, gallery images and event media, stored as absolute URLs.
        Directive("img-src", "'self'", "data:", "https://*.basemaps.cartocdn.com", r2PublicOrigin);

        // Public origin for <video>; the S3 endpoint for the pre-signed album audio URLs.
        // Game music and effects are same-origin under /sound.
        Directive("media-src", "'self'", r2PublicOrigin, r2EndpointOrigin);

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

    /// <summary>
    /// A host optionally followed by a port: DNS name, IPv4, or bracketed IPv6. Deliberately
    /// narrow - the value goes straight into a header, and Kestrel's own Host validation is not
    /// something this needs to rely on.
    /// </summary>
    private static bool IsHostWithOptionalPort(string value)
    {
        var host = value;
        var colon = value.LastIndexOf(':');

        // A bare IPv6 host ends in ']', so a colon after it is the port separator.
        if (colon > 0 && value.IndexOf(']', colon) < 0)
        {
            var port = value.AsSpan(colon + 1);

            if (port.Length is 0 or > 5)
            {
                return false;
            }

            foreach (var c in port)
            {
                if (!char.IsAsciiDigit(c))
                {
                    return false;
                }
            }

            host = value[..colon];
        }

        if (host.Length == 0)
        {
            return false;
        }

        if (host[0] == '[')
        {
            if (host.Length < 3 || host[^1] != ']')
            {
                return false;
            }

            for (var i = 1; i < host.Length - 1; i++)
            {
                var c = host[i];

                if (!char.IsAsciiHexDigit(c) && c != ':' && c != '.')
                {
                    return false;
                }
            }

            return true;
        }

        foreach (var c in host)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '.' && c != '-')
            {
                return false;
            }
        }

        return true;
    }
}
