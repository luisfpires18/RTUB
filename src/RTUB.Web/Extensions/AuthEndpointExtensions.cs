using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;
using RTUB.Security;
using RTUB.Web.Endpoints;
using ApplicationUser = RTUB.Core.Entities.ApplicationUser;

namespace RTUB.Web.Extensions;

// Health, build identity, announcement, login, passkey and logout endpoints.
public static class AuthEndpointExtensions
{
    public static WebApplication MapSystemAndAuthEndpoints(this WebApplication app)
    {
        // --------- Health Checks ---------
        app.MapHealthChecks("/health");

        // --------- Build identity ---------
        // Deploy and rollback smoke tests poll this until the exact version AND commit they just
        // deployed answer, which is what proves the new build is serving rather than an old
        // instance that is still up. Under /api/, so the service worker never caches it.
        app.MapGet("/api/version", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Json(RTUB.Web.Services.BuildInfo.Current);
        }).AllowAnonymous();

        // --------- System announcement (task 037) ---------
        // The Owner's latest announcement while it is fresh (AnnouncementService), for every open React page, signed in or
        // not. 204 when there is none. Polled, so never stored by anything.
        app.MapGet("/api/announcement", (HttpContext context, RTUB.Web.Services.AnnouncementService announcements) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return announcements.Current is { } current ? Results.Json(current) : Results.NoContent();
        }).AllowAnonymous();

        // LOGIN (HTTP POST) — sets cookie, then redirects
        // RequireRateLimiting below caps attempts per client IP; it complements, and does not
        // replace, Identity's per-account lockout. See AddLoginRateLimiting.
        // The IFormCollection parameter makes this endpoint an antiforgery-protected form
        // endpoint: the framework requires a valid token and returns 400 before the handler
        // runs. Do not replace it with HttpContext.Request.ReadFormAsync() — that silently
        // removes CSRF protection. The React /login (React track 007) takes the token from
        // GET /api/public/antiforgery-token and posts with Accept: application/json, so every
        // outcome below answers as JSON ({ error } with 401, or { redirect }) instead of a
        // redirect. Same checks, same order, either way.
        app.MapPost("/auth/login", async (HttpContext context,
                                          IFormCollection form,
                                          SignInManager<ApplicationUser> signInManager,
                                          UserManager<ApplicationUser> userManager,
                                          ApplicationDbContext db,
                                          ILogger<Program> logger,
                                          AuditContext auditContext,
                                          IMemoryCache cache) =>
        {
            var username = form["Username"].ToString();
            var password = form["Password"].ToString();
            var rememberRaw = form["RememberMe"].ToString();
            var remember = bool.TryParse(rememberRaw, out var b) ? b : string.Equals(rememberRaw, "on", StringComparison.OrdinalIgnoreCase);
            var returnUrl = form["ReturnUrl"].ToString();

            var wantsJson = context.Request.GetTypedHeaders().Accept
                .Any(a => a.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase));
            IResult Fail(string error) => wantsJson
                ? Results.Json(new { error }, statusCode: StatusCodes.Status401Unauthorized)
                : Results.Redirect($"/login?error={error}");

            var user = await userManager.FindByNameAsync(username);

            // If not found by username, try to find by email (for users who might enter their email)
            if (user is null && username.Contains("@"))
            {
                user = await userManager.FindByEmailAsync(username);
            }

            if (user is null)
            {
                return Fail("Invalid");
            }

            if (await Program.SignInBlockAsync(user, userManager) is { } blocked)
            {
                return Fail(blocked);
            }

            // Check password is valid BEFORE updating last login date to avoid race condition
            // We need to verify credentials first, then update the timestamp BEFORE signing in
            // to ensure the LastLoginDate is persisted before the user can make any requests
            var passwordValid = await userManager.CheckPasswordAsync(user, password);
            if (!passwordValid)
            {
                // Track failed login attempt for lockout purposes
                await userManager.AccessFailedAsync(user);
                return Fail("Invalid");
            }

            return await Program.CompleteSignInAsync(user, remember, returnUrl, wantsJson, signInManager, userManager, db, logger, auditContext, cache);
        })
        .RequireRateLimiting(RTUB.Web.Extensions.ServiceCollectionExtensions.LoginRateLimitPolicy);

        // Face ID / fingerprint sign-in (task 057): POST /auth/passkey/options + /auth/passkey/login.
        app.MapPasskeyEndpoints();

        // LOGOUT (HTTP POST)
        // The unused IFormCollection parameter is what enables antiforgery validation — see the
        // note on /auth/login. The React member shell posts it (api.ts signOut, token from /api/public/antiforgery-token).
        app.MapPost("/auth/logout", async (IFormCollection form,
                                           SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect("/");
        });

        return app;
    }
}
