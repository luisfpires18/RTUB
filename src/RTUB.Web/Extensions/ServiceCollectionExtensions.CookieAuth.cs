using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Web.Extensions;

// Cookie authentication and the throttled last-activity write.

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Cache-key prefix for the <c>LastLoginDate</c> write throttle, keyed by <b>user id</b>.
    /// Deliberately separate from the authentication-log throttle in the same handler: one limits
    /// how often a line is logged, this one limits how often a row is written.
    /// </summary>
    public const string ActivityWriteCachePrefix = "activity-lastlogin:";

    /// <summary>
    /// How long a successful <c>LastLoginDate</c> write suppresses the next one for the same user.
    /// Well under the one-hour bucket the presence UI renders, so the throttle is invisible there.
    /// </summary>
    public static readonly TimeSpan ActivityWriteThrottle = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Configures cookie authentication events: security-stamp validation, role-change logout,
    /// expulsion check, and session logging, followed by Identity's own
    /// <see cref="SecurityStampValidator"/> so the framework's principal refresh and cookie renewal
    /// still happen. RTUB's checks run on every request; the framework's refresh stays gated by
    /// <see cref="SecurityStampValidatorOptions.ValidationInterval"/>.
    /// </summary>
    public static IServiceCollection AddCookieAuthenticationServices(this IServiceCollection services)
    {
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/login";
            options.Events = new CookieAuthenticationEvents
            {
                OnValidatePrincipal = async context =>
                {
                    var logger = context.HttpContext?.RequestServices?.GetRequiredService<ILogger<Program>>();
                    var cache = context.HttpContext?.RequestServices?.GetService<IMemoryCache>();
                    var userManager = context.HttpContext?.RequestServices?.GetService<UserManager<ApplicationUser>>();
                    var signInManager = context.HttpContext?.RequestServices?.GetService<SignInManager<ApplicationUser>>();

                    if (logger == null || cache == null || userManager == null || signInManager == null)
                        return;

                    var userName = context.Principal?.Identity?.Name;
                    if (string.IsNullOrWhiteSpace(userName))
                        return;

                    var validatedUser = await signInManager.ValidateSecurityStampAsync(context.Principal);
                    if (validatedUser == null)
                    {
                        await signInManager.SignOutAsync();
                        context.RejectPrincipal();
                        return;
                    }

                    if (validatedUser.IsExpelled)
                    {
                        await signInManager.SignOutAsync();
                        context.RejectPrincipal();
                        logger.LogInformation("User {UserName} forced to logout due to expulsion.", userName);
                        return;
                    }

                    // The database is asked only when the cookie claims Admin: the probe exists to catch a revoked
                    // Admin, and a cookie without the claim has nothing to revoke. Every other request skips its two
                    // SELECTs (the role by name, the user-role row).
                    var hasAdminClaim = context.Principal?.IsInRole("Admin") ?? false;
                    if (hasAdminClaim && !await userManager.IsInRoleAsync(validatedUser, "Admin"))
                    {
                        await signInManager.SignOutAsync();
                        context.RejectPrincipal();
                        logger.LogInformation("User {UserName} forced to logout due to role change.", userName);
                        return;
                    }

                    // Identity's own cookie validation, composed with the checks above rather than
                    // replaced by them. AddIdentity installs SecurityStampValidator.ValidatePrincipalAsync
                    // as OnValidatePrincipal; assigning this whole CookieAuthenticationEvents object
                    // drops it unless it is called here, and with it the principal refresh Identity performs after a
                    // successful stamp check — so role and profile claim changes would never reach a live
                    // session and the cookie would never be renewed. This call runs the configured
                    // ISecurityStampValidator, which rebuilds the principal from the database
                    // (SignInManager.CreateUserPrincipalAsync -> ReplacePrincipal + ShouldRenew) once
                    // SecurityStampValidatorOptions.ValidationInterval has elapsed — 30 minutes by
                    // default, deliberately left at the framework default here.
                    //
                    // The order matters and the RTUB checks must stay in front of it:
                    //  - they run on EVERY request, whereas the framework gates its own stamp read
                    //    behind ValidationInterval, so no RTUB check is diluted to that cadence;
                    //  - they read the principal as it arrived in the cookie. A refresh rebuilds it
                    //    from the database, scrubbing exactly the stale "Admin" claim the role probe
                    //    above exists to catch: running the framework first would silently downgrade
                    //    such a session instead of rejecting it, and only on the requests where a
                    //    refresh happened to fall due.
                    //
                    // The framework repeats the stamp check above, but costs nothing for it:
                    // UserManager.GetUserAsync resolves off the request-scoped DbContext's change
                    // tracker, which the check above has already populated. A refresh request pays
                    // only for the rebuild itself — two SELECTs, once per user per ValidationInterval.
                    await SecurityStampValidator.ValidatePrincipalAsync(context);
                    if (context.Principal is null)
                        return;

                    var issuedUtc = context.Properties?.IssuedUtc?.UtcDateTime ?? DateTime.MinValue;
                    var cookieUserAgent = context.HttpContext?.Request?.Headers["User-Agent"].ToString();
                    var logCacheKey = $"login-log:{userName}:{issuedUtc.Ticks}";
                    if (!cache.TryGetValue(logCacheKey, out _))
                    {
                        logger.LogInformation(
                            "User {UserName} authenticated via cookie validation at {LoginTime} ({Device})",
                            userName, DateTime.UtcNow,
                            UserAgentHelper.GetShortUserAgent(cookieUserAgent));
                        cache.Set(logCacheKey, true, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                        });
                    }

                    // LastLoginDate is, despite its name, a last-authenticated-activity stamp: it
                    // backs the online / "Recente" presence UI, which buckets at one hour. This
                    // handler runs on every authenticated request, so the write is throttled to one
                    // per ActivityWriteThrottle per user — far finer than the UI can render, and it
                    // keeps routine requests off the write path entirely.
                    var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.IsNullOrWhiteSpace(userId))
                        return;

                    var activityCacheKey = $"{ActivityWriteCachePrefix}{userId}";
                    if (cache.TryGetValue(activityCacheKey, out _))
                        return;

                    try
                    {
                        var db = context.HttpContext?.RequestServices?.GetService<ApplicationDbContext>();
                        if (db is null)
                        {
                            logger.LogWarning("ApplicationDbContext not available in OnValidatePrincipal");
                            return;
                        }

                        var now = DateTime.UtcNow;
                        for (int attempt = 1; ; attempt++)
                        {
                            try
                            {
                                // Also one LoginEvents "seen" row per member per hour (Owner dashboard:
                                // active members and busy hours). Portugal is a whole-hour offset from UTC, so a
                                // UTC hour is a Portugal hour.
                                var hour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
                                await db.Database.ExecuteSqlInterpolatedAsync($@"
                                    UPDATE AspNetUsers
                                    SET LastLoginDate = {now}
                                    WHERE Id = {userId};
                                    INSERT INTO LoginEvents (UserId, At, IsSignIn)
                                    SELECT {userId}, {now}, 0
                                    WHERE NOT EXISTS (SELECT 1 FROM LoginEvents WHERE UserId = {userId} AND At >= {hour});");
                                break;
                            }
                            // 6 is SQLITE_LOCKED, not SQLITE_BUSY (5). SQLITE_BUSY is already
                            // absorbed by "PRAGMA busy_timeout = 30000" in SqliteConnectionInterceptor;
                            // busy_timeout does not cover SQLITE_LOCKED, which is why it is retried here.
                            catch (Microsoft.Data.Sqlite.SqliteException ex) when (attempt < 3 && ex.SqliteErrorCode == 6)
                            {
                                await Task.Delay(50 * (int)Math.Pow(2, attempt - 1));
                            }
                        }

                        // Only once the write has actually succeeded — a failed update must not
                        // suppress the next request's attempt.
                        cache.Set(activityCacheKey, true, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = ActivityWriteThrottle
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error while updating LastLoginDate for {UserName}", userName);
                    }
                }
            };
        });

        return services;
    }
}
