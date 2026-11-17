using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Services.Geocoding;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;
using RTUB.Security;
using RTUB.Web.Extensions;
using ApplicationUser = RTUB.Core.Entities.ApplicationUser;

namespace RTUB.Web.Extensions;

// Error page, HSTS and the central security headers (CSP on HTML documents).
public static class SecurityHeadersExtensions
{
    public static WebApplication UseErrorHandlingAndSecurityHeaders(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        // --------- Security headers ---------
        // One central place. Runs before static files and the endpoints, so every response carries these. It also runs again when
        // UseExceptionHandler (registered above) re-executes the pipeline, which is why the
        // headers are assigned by indexer rather than appended - reassignment is idempotent.
        //
        // Strict-Transport-Security is NOT set here: UseHsts above already emits it in every
        // non-Development environment, and production returns max-age=2592000 today.
        //
        // Content-Security-Policy is ENFORCED (unit 025), with no 'unsafe-inline' and no
        // 'unsafe-eval': unit 022 removed all 15 JSRuntime.InvokeAsync("eval", ...) calls,
        // unit 023 removed every inline <script> block and inline on* handler attribute, and
        // unit 024 removed every inline <style> block and style="..." attribute from
        // browser-served markup. Every source in the policy is evidence-based - see
        // ContentSecurityPolicyBuilder and .ai/STATE.md for the per-directive justification.
        //
        // It is emitted on HTML DOCUMENT responses only, deliberately. A CSP header served with
        // a worker script governs that worker's own execution context, and RTUB's service
        // worker re-fetches the cross-origin subresources it caches (R2 media, the script/style
        // CDNs, the Leaflet tiles) - none of which connect-src lists, because the page itself
        // never fetches them. A blanket policy would therefore break offline caching. On other
        // subresource responses the header buys nothing: the directives that matter are already
        // enforced by the embedding document's own policy at fetch time, and frame-ancestors
        // applies only to documents (X-Frame-Options: DENY above covers every response anyway).
        //
        // Set from OnStarting because Content-Type is not known when this middleware runs.
        var contentSecurityPolicy = new ContentSecurityPolicyBuilder(app.Configuration);

        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            // The app serves user-uploaded media and JSON/manifest documents; stop MIME sniffing.
            headers["X-Content-Type-Options"] = "nosniff";

            // Full URL to same-origin, origin only to other https origins, nothing on downgrade.
            // Matters for the target="_blank" links out to YouTube/Spotify.
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // RTUB never embeds itself: no window.top/window.parent logic, the PWA is
            // display:standalone and the Android TWA uses a Custom Tab, not a frame. The app's
            // own <iframe>s embed R2-hosted PDFs, whose headers are R2's, not these. So DENY is
            // safe and strictly better than SAMEORIGIN.
            headers["X-Frame-Options"] = "DENY";

            // Only features verified unused across wwwroot/js, Pages and Shared. `fullscreen`
            // (PDF viewer iframes use allow="fullscreen") and `clipboard-write`
            // (clipboardCopy.js) are in use and are deliberately left alone, as is
            // the long tail of exotic features, where a blanket deny buys nothing measurable.
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            context.Response.OnStarting(static state =>
            {
                var (ctx, policy) = ((HttpContext, ContentSecurityPolicyBuilder))state;
                var contentType = ctx.Response.ContentType;

                if (contentType is not null &&
                    contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    // Assigned, never appended: UseExceptionHandler re-executes the pipeline and
                    // registers this callback a second time on the same response.
                    ctx.Response.Headers["Content-Security-Policy"] =
                        policy.Build();
                }

                return Task.CompletedTask;
            }, (context, contentSecurityPolicy));

            await next();
        });

        return app;
    }
}
