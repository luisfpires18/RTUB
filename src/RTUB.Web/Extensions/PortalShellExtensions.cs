namespace RTUB.Web.Extensions;

// React shell routes, /Error, legacy redirects and /login.
public static class PortalShellExtensions
{
    public static WebApplication MapPortalShell(this WebApplication app)
    {
        // --------- React shell routes ---------
        // React owns exactly the paths mapped from here down to /login: public ones first, then the members-only ones
        // that send visitors to sign in. /Error is a static server page; nothing else renders HTML. Old addresses
        // redirect to their page today (MapLegacyRedirects). List: docs/react-portal-pilot.md.
        // The hashed /portal/assets/* are ordinary static files (cached above); the shell itself is no-cache so a
        // deploy is picked up at once.
        var portalShell = new StaticFileOptions
        {
            OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
        };
        foreach (var route in new[] { "/", "/privacy", "/profile", "/request" })
        {
            app.MapFallbackToFile(route, "portal/index.html", portalShell)
               .WithMetadata(new HttpMethodMetadata(["GET", "HEAD"]));
        }

        // Public pages. /share is the PWA Web Share Target (manifest share_target, GET only, so a POST answers 405).
        foreach (var route in new[] { "/music", "/music/albums/{id:int}", "/roles", "/gallery", "/shop",
                     "/events", "/events/{id:int}", "/news", "/share" })
        {
            app.MapFallbackToFile(route, "portal/index.html", portalShell)
               .WithMetadata(new HttpMethodMetadata(["GET", "HEAD"]));
        }

        // Members-only pages: a visitor goes to sign in and comes back to the same address (query included). The API
        // decides everything else (who may see or change what).
        foreach (var route in new[] { "/events/{id:int}/discussion", "/events/{id:int}/contacts", "/rehearsals", "/rehearsals/{id:int}",
                     "/members", "/members/hierarchy", "/leaderboard", "/inventory", "/documentation", "/logistics", "/logistics/tasks", "/logistics/{id:int}",
                     "/treasury", "/treasury/reports/{id:int}", "/treasury/calotes", "/treasury/mbway", "/treasury/nerba", "/treasury/nerba/{id:int}",
                     "/pending-actions", "/requests", "/questions", "/members/map", "/hall-of-fame", "/naipes", "/naipes/config",
                     "/music/play/{id:int}", "/music/learn", "/meetings/polls", "/meetings", "/messages" })
        {
            app.MapMethods(route, ["GET", "HEAD"], (HttpContext context, IWebHostEnvironment env) =>
            {
                if (context.User.Identity?.IsAuthenticated != true)
                {
                    // 069: the query comes back too (/logistics/{id}?card=, /messages?c=); /login keeps local targets only.
                    return Results.Redirect("/login?returnUrl=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
                }

                context.Response.Headers.CacheControl = "no-cache";
                return Results.File(env.WebRootFileProvider.GetFileInfo("portal/index.html").PhysicalPath!, "text/html");
            });
        }

        // Owner tools, Owner only. A visitor goes to sign in; any other member gets the shell with 403 and the page
        // shows the refusal. The API refuses them every call (OwnerEndpoints).
        foreach (var route in new[] { "/emails", "/notifications", "/users", "/owner/tracing", "/owner/dashboard", "/owner/db" })
        {
            app.MapMethods(route, ["GET", "HEAD"], (HttpContext context, IWebHostEnvironment env) =>
            {
                if (context.User.Identity?.IsAuthenticated != true)
                {
                    return Results.Redirect("/login?returnUrl=" + Uri.EscapeDataString(context.Request.Path));
                }

                context.Response.Headers.CacheControl = "no-cache";
                var shell = env.WebRootFileProvider.GetFileInfo("portal/index.html").PhysicalPath!;
                return context.User.IsInRole("Owner")
                    ? Results.File(shell, "text/html")
                    : Results.Content(File.ReadAllText(shell), "text/html", System.Text.Encoding.UTF8, StatusCodes.Status403Forbidden);
            });
        }

        // /forgot-password, /reset-password and /confirm-email are public. The links carry Identity tokens in the query,
        // so the shell is never stored (no-store) and the page never sends its address on (no-referrer); the page
        // clears the query from the address bar once read.
        foreach (var route in new[] { "/forgot-password", "/reset-password", "/confirm-email" })
        {
            app.MapMethods(route, ["GET", "HEAD"], (HttpContext context, IWebHostEnvironment env) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                return Results.File(env.WebRootFileProvider.GetFileInfo("portal/index.html").PhysicalPath!, "text/html");
            });
        }

        // /Error stays server-owned on purpose (not React). UseExceptionHandler re-executes the
        // failed request here with its own method (GET, POST, PUT, ...), and an error page must not depend on the portal
        // bundle, a session or the database. A fixed static page (wwwroot/error.html: no script, no request details,
        // no stack trace); the handler's status (500) is kept. docs/react-identity.md.
        app.Map("/Error", async (HttpContext context, IWebHostEnvironment env) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var page = await File.ReadAllTextAsync(env.WebRootFileProvider.GetFileInfo("error.html").PhysicalPath!);
            return Results.Content(page, "text/html", System.Text.Encoding.UTF8);
        });

        app.MapLegacyRedirects();

        // React Login (track 007). Everyone signed out gets the React shell, like the routes above.
        // A signed-in member never sees the form and goes to the members' landing page. The return
        // URL is deliberately ignored here: /login is also the cookie's AccessDeniedPath, so a member
        // bounced off a page their role cannot open would be sent straight back to it, in a loop.
        app.MapMethods("/login", ["GET", "HEAD"], async (HttpContext context, IWebHostEnvironment env) =>
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                return Results.Redirect(Program.MemberLanding(null));
            }

            context.Response.Headers.CacheControl = "no-cache";
            return Results.File(env.WebRootFileProvider.GetFileInfo("portal/index.html").PhysicalPath!, "text/html");
        });

        return app;
    }
}
