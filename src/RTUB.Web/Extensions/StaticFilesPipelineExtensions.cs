namespace RTUB.Web.Extensions;

// HTTPS redirect, compression, routing, rate limiter, static files and authentication middleware.
public static class StaticFilesPipelineExtensions
{
    public static WebApplication UseRoutingStaticFilesAndAuth(this WebApplication app)
    {
        // Only use HTTPS redirection in development
        // In production (Azure App Service), HTTPS is handled at the load balancer level
        if (app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        // Enable response compression (must be before UseStaticFiles)
        // Only enable in production to avoid conflicts with BrowserLink/BrowserRefresh dev tools
        if (!app.Environment.IsDevelopment())
        {
            app.UseResponseCompression();
        }

        // Enable response caching
        app.UseResponseCaching();

        app.UseRouting();

        // Must follow UseRouting so the endpoint's RequireRateLimiting metadata is resolved, and
        // precedes authentication/antiforgery so a throttled client is answered 429 before any
        // credential or token work is done.
        app.UseRateLimiter();

        // Static files, /images/* included: ETag, Last-Modified and 304 come from the middleware itself. The
        // ImagesController that re-implemented them for /images left in task 054, and with it the cookie validation
        // (three SELECTs) every image request paid by running after UseAuthentication.
        // Configure content type provider to serve .webmanifest and .well-known files with correct MIME types
        var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        provider.Mappings[".webmanifest"] = "application/manifest+json";
        // Ensure .well-known/assetlinks.json is served as application/json for Android Digital Asset Links
        provider.Mappings[".json"] = "application/json";

        app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
        {
            ContentTypeProvider = provider,
            OnPrepareResponse = ctx =>
            {
                var path = ctx.Context.Request.Path.Value?.ToLowerInvariant() ?? "";

                // Digital Asset Links for Android TWA - minimal caching for verification
                // FileExtensionContentTypeProvider handles Content-Type automatically
                if (path.Equals("/.well-known/assetlinks.json"))
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=3600");
                }
                // Service Worker must always be revalidated so browsers detect updates immediately
                // Without this, the SW file gets the 30-day cache and users see stale UI
                else if (path.EndsWith("service-worker.js"))
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "no-cache");
                }
                // PWA icons and manifest should have shorter cache to allow updates
                else if (path.Contains("/icons/") || path.EndsWith("manifest.json") || path.EndsWith("manifest.webmanifest") || path.StartsWith("/apple-touch-icon"))
                {
                    // Cache for 1 hour with must-revalidate to ensure updates are picked up
                    ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=3600,must-revalidate");
                }
                // /images/* (brand assets, the default avatar): revalidated through the middleware's ETag, as the
                // retired ImagesController answered (task 054). The service worker keeps its own cache-first copy.
                else if (path.StartsWith("/images/"))
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "no-cache");
                }
                // Cache other static files for 30 days in production. The React build's content-hashed
                // /portal/assets/* never change under their names, so browsers may skip revalidating them (task 054).
                else if (!app.Environment.IsDevelopment())
                {
                    ctx.Context.Response.Headers.Append("Cache-Control",
                        path.StartsWith("/portal/assets/") ? "public,max-age=2592000,immutable" : "public,max-age=2592000");
                }
            }
        });

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        return app;
    }
}
