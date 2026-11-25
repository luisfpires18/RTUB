namespace RTUB.Web.Extensions;

/// <summary>
/// One address for the site (task 163). When <c>App:BaseUrl</c> is a <c>www.</c> address, a request to the same domain
/// without <c>www.</c> is sent there permanently (308: the method and body are kept), with the path and query.
/// rtub.pt and www.rtub.pt are two origins to a browser: separate cookies and storage, and a passkey only works on the
/// domain it was created on, so Face ID on rtub.pt found no key. Any other host (DEV, a local run, Azure probes) passes.
/// </summary>
public static class CanonicalHostExtensions
{
    public static WebApplication UseCanonicalHost(this WebApplication app)
    {
        if (CanonicalFor(app.Configuration["App:BaseUrl"]) is not { } canonical)
        {
            return app;
        }

        var bare = canonical.Host[4..];
        app.Use(async (context, next) =>
        {
            if (string.Equals(context.Request.Host.Host, bare, StringComparison.OrdinalIgnoreCase))
            {
                var request = context.Request;
                context.Response.StatusCode = StatusCodes.Status308PermanentRedirect;
                context.Response.Headers.Location =
                    $"{canonical.Scheme}://{canonical.Authority}{request.PathBase}{request.Path}{request.QueryString}";
                return;
            }

            await next();
        });
        return app;
    }

    /// <summary>The configured base address when it is an absolute <c>www.</c> address; otherwise null (no redirect).</summary>
    internal static Uri? CanonicalFor(string? baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
        && uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
        && uri.Host.Length > 4
            ? uri
            : null;
}
