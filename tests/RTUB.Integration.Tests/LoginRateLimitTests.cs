using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Integration tests for the per-client rate limit on <c>POST /auth/login</c>.
/// </summary>
/// <remarks>
/// Every test uses its own client IP, so each one starts with a full, private permit budget and
/// the class is order-independent without waiting for a window to roll over.
/// </remarks>
public class LoginRateLimitTests : IntegrationTestBase
{
    /// <summary>
    /// Mirrors the shipped <c>LoginRateLimit:PermitLimit</c> default. If the default is retuned
    /// this class fails, which is deliberate: the threshold is a security decision, not a detail.
    /// </summary>
    private const int PermitLimit = 10;

    private const string IdentityCookieName = ".AspNetCore.Identity.Application";

    public LoginRateLimitTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task LoginPost_UnderLimit_SignsUserInNormally()
    {
        var client = CreateBrowserClient("203.0.113.10");
        var password = NewSecret();
        var user = await CreateUserAsync("rl-under-limit", password);

        var response = await PostLoginWithTokenAsync(client, user.UserName!, password);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/");
        SetCookieHeaders(response).Should().Contain(c => c.Contains(IdentityCookieName),
            "a login inside the limit must behave exactly as it did before rate limiting");
    }

    [Fact]
    public async Task LoginPost_AtTheLimit_IsStillAccepted_ThenRejectedWith429()
    {
        var client = CreateBrowserClient("203.0.113.20");

        // The limiter runs before antiforgery, so every POST spends a permit whatever its outcome.
        // Tokenless posts are the cheapest way to spend the budget and prove that ordering.
        for (var i = 1; i <= PermitLimit; i++)
        {
            var allowed = await client.PostAsync("/auth/login", TokenlessLoginForm());
            allowed.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                $"request {i} is inside the limit, so it must reach antiforgery rather than be throttled");
        }

        var rejected = await client.PostAsync("/auth/login", TokenlessLoginForm());

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task LoginPost_WhenRejected_SendsRetryAfterFromLeaseMetadata()
    {
        var client = CreateBrowserClient("203.0.113.30");
        await ExhaustPermitsAsync(client);

        var rejected = await client.PostAsync("/auth/login", TokenlessLoginForm());

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.Should().NotBeNull("the fixed window lease exposes RetryAfter metadata");
        rejected.Headers.RetryAfter!.Delta.Should().NotBeNull();
        rejected.Headers.RetryAfter!.Delta!.Value.Should().BeGreaterThan(TimeSpan.Zero,
            "Retry-After must tell the client when the window reopens");
    }

    [Fact]
    public async Task LoginPost_WhenRejected_DoesNotSignAnyoneIn()
    {
        var client = CreateBrowserClient("203.0.113.40");
        var password = NewSecret();
        var user = await CreateUserAsync("rl-rejected-no-cookie", password);

        await ExhaustPermitsAsync(client);

        // Valid credentials and a real token, but the budget is gone: no cookie may be issued.
        var rejected = await PostLoginWithTokenAsync(client, user.UserName!, password);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        SetCookieHeaders(rejected).Should().NotContain(c => c.Contains(IdentityCookieName));
    }

    [Fact]
    public async Task LoginRateLimit_IsPartitionedByClientIp()
    {
        var throttled = CreateBrowserClient("203.0.113.50");
        var untouched = CreateBrowserClient("203.0.113.51");

        await ExhaustPermitsAsync(throttled);
        (await throttled.PostAsync("/auth/login", TokenlessLoginForm()))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var other = await untouched.PostAsync("/auth/login", TokenlessLoginForm());

        other.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "one exhausted client must not spend another client's budget");
    }

    [Fact]
    public async Task LoginPost_WithWrongPassword_UnderLimit_StillCountsTowardAccountLockout()
    {
        var client = CreateBrowserClient("203.0.113.60");
        var user = await CreateUserAsync("rl-lockout-preserved", NewSecret());

        var response = await PostLoginWithTokenAsync(client, user.UserName!, NewSecret());

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/login?error=Invalid");

        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var reloaded = await userManager.FindByNameAsync(user.UserName!);

        (await userManager.GetAccessFailedCountAsync(reloaded!)).Should().Be(1,
            "per-account lockout must keep working alongside the per-IP limit");
    }

    [Fact]
    public async Task LoginPost_UnderLimit_StillRequiresAntiforgeryToken()
    {
        var client = CreateBrowserClient("203.0.113.70");
        var password = NewSecret();
        var user = await CreateUserAsync("rl-antiforgery-intact", password);

        var response = await client.PostAsync("/auth/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Username"] = user.UserName!,
                ["Password"] = password,
                ["RememberMe"] = "false"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        SetCookieHeaders(response).Should().NotContain(c => c.Contains(IdentityCookieName));
    }

    [Fact]
    public async Task RateLimiting_IsScopedToLoginOnly()
    {
        var client = CreateBrowserClient("203.0.113.80");
        var overLimit = PermitLimit + 2;

        // There is no GlobalLimiter, so nothing outside the login POST may ever be throttled.
        for (var i = 1; i <= overLimit; i++)
        {
            (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/login")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.PostAsync("/auth/logout", TokenlessLogoutForm()))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest,
                    "logout stays antiforgery-protected and unthrottled");
        }
    }

    /// <summary>Spends the whole budget for one client without asserting on the responses.</summary>
    private static async Task ExhaustPermitsAsync(HttpClient client)
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            (await client.PostAsync("/auth/login", TokenlessLoginForm())).Dispose();
        }
    }

    private HttpClient CreateBrowserClient(string clientIp)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, clientIp);
        return client;
    }

    /// <summary>Drives the real browser flow: GET the page, read its token, POST the form.</summary>
    private static async Task<HttpResponseMessage> PostLoginWithTokenAsync(
        HttpClient client, string userName, string password)
    {
        var page = await client.GetAsync("/login");
        page.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = AntiforgeryFormToken.Find(await page.Content.ReadAsStringAsync());
        token.Should().NotBeNullOrEmpty("/login must render a framework antiforgery token");

        return await client.PostAsync("/auth/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                [AntiforgeryFormToken.FieldName] = token!,
                ["Username"] = userName,
                ["Password"] = password,
                ["RememberMe"] = "false"
            }));
    }

    private async Task<ApplicationUser> CreateUserAsync(string userName, string password)
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName + "@test.com",
            EmailConfirmed = true,
            FirstName = "Rate",
            LastName = "Limit",
            Nickname = userName,
            PhoneNumber = "123456789"
        };

        (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        return user;
    }

    /// <summary>
    /// Generated per call so no credential-shaped literal is ever committed. Identity is configured
    /// with <c>RequiredLength = 4</c> and no complexity rules, so this satisfies the policy.
    /// </summary>
    private static string NewSecret() => Guid.NewGuid().ToString("N");

    /// <summary>A login body with no antiforgery token: spends a permit, then fails validation.</summary>
    private static FormUrlEncodedContent TokenlessLoginForm() => new(
        new Dictionary<string, string> { ["Username"] = "rl-probe", ["RememberMe"] = "false" });

    private static FormUrlEncodedContent TokenlessLogoutForm() => new(
        new Dictionary<string, string> { ["_handler"] = "LogoutForm" });

    private static IEnumerable<string> SetCookieHeaders(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : Array.Empty<string>();
}
