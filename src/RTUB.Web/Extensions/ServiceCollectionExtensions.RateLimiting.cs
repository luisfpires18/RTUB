using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace RTUB.Web.Extensions;

// Rate limiting policies: login and public requests.

public static partial class ServiceCollectionExtensions
{
    /// <summary>Name of the rate limiting policy applied to <c>POST /auth/login</c>.</summary>
    public const string LoginRateLimitPolicy = "login";

    private const string LoginRateLimitSection = "LoginRateLimit";

    /// <summary>
    /// Login attempts allowed per client IP per window. Identity locks an account after 5 failures,
    /// so this sits just above that: a single fumbling user is never throttled, while one client
    /// probing a list of accounts is capped well below a useful credential-stuffing rate.
    /// </summary>
    private const int DefaultLoginPermitLimit = 10;

    /// <summary>Matched to <c>Lockout.DefaultLockoutTimeSpan</c> so both limits tell one story.</summary>
    private const double DefaultLoginWindowMinutes = 5;

    /// <summary>Name of the rate limiting policy applied to <c>POST /api/public/requests</c>.</summary>
    public const string PublicRequestRateLimitPolicy = "public-requests";

    private const string PublicRequestRateLimitSection = "PublicRequestRateLimit";

    /// <summary>
    /// Anonymous performance requests allowed per client IP per window. Each one saves a row, pushes
    /// to every Admin and Owner and emails RTUB; a person books a performance once, so a handful per
    /// hour never gets in the way of a real request and caps a script well below nuisance level.
    /// </summary>
    private const int DefaultPublicRequestPermitLimit = 5;

    private const double DefaultPublicRequestWindowMinutes = 60;

    /// <summary>
    /// Registers the per-client rate limiter for <c>POST /auth/login</c>.
    /// </summary>
    /// <remarks>
    /// This is a named policy only - there is deliberately no <c>GlobalLimiter</c>, so nothing is
    /// throttled except the endpoints that opt in with <c>RequireRateLimiting</c>.
    ///
    /// It complements, and does not replace, Identity's per-account lockout
    /// (<see cref="AddIdentityServices"/>): lockout stops repeated guesses against one account,
    /// this stops one client walking many accounts (credential stuffing), which lockout never sees.
    ///
    /// Partitioning is on <c>Connection.RemoteIpAddress</c> only. The request's own headers are
    /// never read: <c>X-Forwarded-For</c> is caller-controlled, so trusting it here would let an
    /// attacker mint a fresh budget per request and allocate a limiter per forged value. Behind a
    /// reverse proxy, <c>RemoteIpAddress</c> must be corrected by the host (on Azure App Service,
    /// the <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c> app setting), not by parsing headers here.
    /// </remarks>
    public static IServiceCollection AddLoginRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(LoginRateLimitSection);
        var permitLimit = section.GetValue("PermitLimit", DefaultLoginPermitLimit);
        var window = TimeSpan.FromMinutes(section.GetValue("WindowMinutes", DefaultLoginWindowMinutes));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(LoginRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    // A null RemoteIpAddress collapses into this one shared bucket rather than
                    // creating a partition, so the partition count stays bounded by the number of
                    // real peers and can never be grown by anything a caller supplies.
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window,
                        // No queue: a rejected attempt is answered immediately instead of holding
                        // the request open, which is what a flood would otherwise exploit.
                        QueueLimit = 0
                    }));

            // One handler for every policy, so it answers per policy: the login form keeps its plain
            // text, the public request API gets problem JSON that the React form can read.
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                }

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
                if (policy == PublicRequestRateLimitPolicy)
                {
                    await context.HttpContext.Response.WriteAsJsonAsync(
                        new Microsoft.AspNetCore.Mvc.ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Recebemos vários pedidos seguidos deste dispositivo. Tente novamente mais tarde."
                        },
                        (System.Text.Json.JsonSerializerOptions?)null,
                        "application/problem+json",
                        cancellationToken);
                    return;
                }

                await context.HttpContext.Response.WriteAsync(
                    "Demasiadas tentativas de login. Tente novamente mais tarde.", cancellationToken);
            };
        });

        return services;
    }

    /// <summary>
    /// Registers the per-client rate limiter for <c>POST /api/public/requests</c>.
    /// Same partitioning as <see cref="AddLoginRateLimiting"/> - <c>RemoteIpAddress</c> only, never a
    /// caller-supplied header - with its own budget and its own rejection body (see OnRejected there).
    /// </summary>
    public static IServiceCollection AddPublicRequestRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(PublicRequestRateLimitSection);
        var permitLimit = section.GetValue("PermitLimit", DefaultPublicRequestPermitLimit);
        var window = TimeSpan.FromMinutes(section.GetValue("WindowMinutes", DefaultPublicRequestWindowMinutes));

        services.AddRateLimiter(options =>
            options.AddPolicy(PublicRequestRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window,
                        QueueLimit = 0
                    })));

        return services;
    }
}
