using System.Buffers.Text;

namespace RTUB.Application.Helpers;

/// <summary>
/// What a browser push subscription must look like before it is stored (task 038, docs/react-push-v2.md). The server
/// later POSTs every notification to the endpoint, so it must be an https URL on a known browser push service (no
/// arbitrary host, no IP address: the server would otherwise make requests wherever a member pointed it). The keys
/// are the browser's P-256 public key (65 bytes, uncompressed) and its 16-byte auth secret, base64url (standard
/// base64 is accepted too, some Android browsers send it).
/// </summary>
public static class PushSubscriptionRules
{
    public const int MaxEndpointLength = 2048;

    /// <summary>
    /// The browsers' push services: Chrome / Edge on Android / Samsung / Opera (FCM), Firefox (Mozilla autopush),
    /// Safari (Apple), Edge on Windows (WNS). A browser on another service is refused with a clear message.
    /// </summary>
    public static readonly string[] PushServiceHosts =
    [
        "fcm.googleapis.com",
        "android.googleapis.com",
        "push.services.mozilla.com",
        "push.apple.com",
        "notify.windows.com",
    ];

    /// <summary>Field errors (endpoint, p256dh, auth) in pt-PT; empty when the subscription is usable.</summary>
    public static Dictionary<string, string[]> Validate(string? endpoint, string? p256dh, string? auth)
    {
        var errors = new Dictionary<string, string[]>();

        if (!IsPushServiceEndpoint(endpoint))
        {
            errors["endpoint"] = ["O navegador não deu um endereço de notificações válido."];
        }

        if (DecodedLength(p256dh) is not 65 || DecodeFirstByte(p256dh) != 0x04)
        {
            errors["p256dh"] = ["A chave da subscrição não é válida."];
        }

        if (DecodedLength(auth) is not 16)
        {
            errors["auth"] = ["A chave da subscrição não é válida."];
        }

        return errors;
    }

    /// <summary>An absolute https URL, no user info, on one of <see cref="PushServiceHosts"/> (or a subdomain).</summary>
    public static bool IsPushServiceEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > MaxEndpointLength ||
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || uri.HostNameType != UriHostNameType.Dns)
        {
            return false;
        }

        var host = uri.IdnHost.ToLowerInvariant();
        return PushServiceHosts.Any(h => host == h || host.EndsWith("." + h, StringComparison.Ordinal));
    }

    private static int? DecodedLength(string? value) => Decode(value)?.Length;

    private static byte? DecodeFirstByte(string? value) => Decode(value) is { Length: > 0 } bytes ? bytes[0] : null;

    private static byte[]? Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
        {
            return null;
        }

        var url = value.Trim().Replace('+', '-').Replace('/', '_').TrimEnd('=');
        try
        {
            return Base64Url.DecodeFromChars(url);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
