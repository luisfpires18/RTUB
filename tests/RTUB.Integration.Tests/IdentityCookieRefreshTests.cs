using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// The other half of <see cref="CookieValidationTests"/>: what Identity's own cookie validation
/// contributes once <see cref="SecurityStampValidatorOptions.ValidationInterval"/> has elapsed.
///
/// <para>Unit 020 stopped RTUB's <c>OnValidatePrincipal</c> from <b>replacing</b>
/// <c>SecurityStampValidator.ValidatePrincipalAsync</c> and made it <b>call</b> it, after its own
/// checks. Before that, the framework's success path — <c>CreateUserPrincipalAsync</c> ->
/// <c>ReplacePrincipal</c> + <c>ShouldRenew</c> — never ran at all, so a live session's claims were
/// frozen at the moment the cookie was issued and the cookie was never renewed.</para>
///
/// <para>These tests run against a host whose validator clock is pushed past the interval, so the
/// refresh is due on every request. That is also what makes them the ordering proof: RTUB's
/// rejections must still fire <b>first</b>, on the principal as it arrived, even when a refresh
/// would otherwise have rebuilt it from the database and scrubbed the very claim being checked.</para>
/// </summary>
public class IdentityCookieRefreshTests : IClassFixture<RefreshingCookieFactory>
{
    private const string RoleLookup = "\"AspNetRoles\"";
    private const string UserRoleLookup = "\"AspNetUserRoles\"";
    private const string UserClaimLookup = "\"AspNetUserClaims\"";

    private readonly RefreshingCookieFactory _factory;

    public IdentityCookieRefreshTests(RefreshingCookieFactory factory)
    {
        _factory = factory;
    }

    // ------------------------------------------------------- framework renewal / claim refresh

    [Fact]
    public async Task ValidationIntervalElapsed_RenewsTheCookie_AndKeepsTheSessionAuthenticated()
    {
        var (client, _) = await CookieTestSession.SignInAsync(_factory, "refresh-renew", "10.31.0.1");

        var response = await client.GetAsync("/Events");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a refresh renews the session, it does not end it");
        CookieTestSession.DeletesIdentityCookie(response).Should().BeFalse();
        CookieTestSession.RenewsIdentityCookie(response).Should().BeTrue(
            "SecurityStampVerified sets ShouldRenew, and the cookie handler writes the replaced " +
            "principal back out as a new Set-Cookie");
    }

    [Fact]
    public async Task RoleAddedWithoutStampChange_ReachesTheLiveSession_AtTheNextValidation()
    {
        // Signed in with no role at all, so the cookie's principal carries no "Owner" claim.
        var (client, user) = await CookieTestSession.SignInAsync(_factory, "refresh-claims", "10.31.0.2");

        var denied = await PostBroadcastAsync(client);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the session is authenticated, so [Authorize(Roles = \"Owner\")] forbids rather than challenges");

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(user.Id);

            // The Identity-supported mutation for a *refresh* is the role change on its own.
            // AddToRoleAsync deliberately leaves the security stamp alone; bumping the stamp is the
            // revocation path and forces a re-login instead — that is
            // CookieValidationTests.SecurityStampChange_RejectsCookie, not this.
            (await userManager.AddToRoleAsync(stored!, "Owner")).Succeeded.Should().BeTrue();
        }

        // No new login, no manual claim surgery: the next validation rebuilds the principal from
        // the database, and authorization in that same request sees the new role.
        var allowed = await PostBroadcastAsync(client);

        allowed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await allowed.Content.ReadAsStringAsync()).Should().Contain("Web Push is not configured",
            "reaching the action body at all proves the Owner role claim is now on the principal");
    }

    // ------------------------------------------- RTUB checks still fire first, and immediately

    [Fact]
    public async Task AdminRoleRemoved_IsStillRejectedImmediately_WhileRefreshIsDue()
    {
        var (client, user) = await CookieTestSession.SignInAsync(
            _factory, "refresh-admin", "10.31.0.3", role: "Admin");

        // Arm the unit 019 activity-write throttle, so the rejecting request is one that skips the
        // LastLoginDate write.
        (await client.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(user.Id);
            (await userManager.RemoveFromRoleAsync(stored!, "Admin")).Succeeded.Should().BeTrue();
        }

        // This is the ordering proof. A refresh is due on this very request; had the framework run
        // first it would have rebuilt the principal without the Admin claim, RTUB's
        // "cookie says Admin, database does not" probe would have found nothing, and the session
        // would have been quietly downgraded instead of rejected.
        var rejecting = await client.GetAsync("/Events");
        CookieTestSession.DeletesIdentityCookie(rejecting).Should().BeTrue(
            "the Admin consistency check must keep rejecting on the next request, whatever the " +
            "framework's validation cadence is");
    }

    [Fact]
    public async Task ExpelledUser_IsStillRejectedImmediately_WhileRefreshIsDue()
    {
        var (client, user) = await CookieTestSession.SignInAsync(_factory, "refresh-expelled", "10.31.0.4");

        (await client.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(user.Id);
            stored!.IsExpelled = true;
            // UpdateAsync leaves the security stamp valid, so the framework validator would happily
            // refresh this principal. Expulsion is RTUB's rule alone and runs before it.
            (await userManager.UpdateAsync(stored)).Succeeded.Should().BeTrue();
        }

        var rejecting = await client.GetAsync("/Events");
        CookieTestSession.DeletesIdentityCookie(rejecting).Should().BeTrue();
    }

    [Fact]
    public async Task SecurityStampChange_IsStillRejectedImmediately_WhileRefreshIsDue()
    {
        var (client, user) = await CookieTestSession.SignInAsync(_factory, "refresh-stamp", "10.31.0.5");

        (await client.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(user.Id);
            (await userManager.UpdateSecurityStampAsync(stored!)).Succeeded.Should().BeTrue();
        }

        var rejecting = await client.GetAsync("/Events");
        CookieTestSession.DeletesIdentityCookie(rejecting).Should().BeTrue();
    }

    // ------------------------------------------------------------------- cost of a refresh pass

    [Fact]
    public async Task RefreshRequest_StillWritesLastLoginDate_OnlyOncePerThrottleWindow()
    {
        var (client, _) = await CookieTestSession.SignInAsync(_factory, "refresh-cost", "10.31.0.6");

        using (_factory.Sql.Recording())
        {
            for (var i = 0; i < 5; i++)
            {
                (await client.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }

        _factory.Sql.Count(CookieTestSession.LastLoginWrite).Should().Be(1,
            "restoring the framework refresh must not reopen the unit 019 write throttle");
    }

    [Fact]
    public async Task RefreshRequest_CostsTwoExtraSelects_ToRebuildThePrincipal()
    {
        var (client, _) = await CookieTestSession.SignInAsync(_factory, "refresh-extra", "10.31.0.7");
        (await client.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (_factory.Sql.Recording())
        {
            (await client.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Inside the interval this request costs 1 and 1 (CookieValidationTests pins that). The
        // refresh adds CreateUserPrincipalAsync's own reads: one role join and one user-claims
        // SELECT — two commands, once per user per ValidationInterval.
        _factory.Sql.Count(RoleLookup).Should().Be(2,
            "RTUB's Admin probe, plus the role join CreateUserPrincipalAsync runs");
        _factory.Sql.Count(UserRoleLookup).Should().Be(2);
        _factory.Sql.Count(UserClaimLookup).Should().Be(1,
            "the rebuilt principal also picks up the user's own claims");

        // Two, not three: the framework's own VerifySecurityStamp repeats RTUB's stamp read but
        // adds no SELECT for it, because UserManager.GetUserAsync resolves off the request-scoped
        // DbContext's change tracker that RTUB's check has already populated.
    }

    private static Task<HttpResponseMessage> PostBroadcastAsync(HttpClient client)
    {
        // Web Push is unconfigured under the test host, so an authorized call stops at the
        // IsConfigured() guard. That is deliberate: the status alone separates "forbidden" from
        // "reached the action", without sending anything.
        var content = new StringContent("""{"Title":"t","Body":"b"}""", Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return client.PostAsync("/api/push/broadcast", content);
    }
}

/// <summary>
/// <see cref="CookieValidationFactory"/> — same database, same SQL recorder — with the security
/// stamp validator's clock pushed past <see cref="SecurityStampValidatorOptions.ValidationInterval"/>,
/// so every request is one on which Identity's refresh falls due.
///
/// <para>The clock is moved rather than the interval, so the tests exercise the real, unmodified
/// 30-minute framework default instead of a value invented for them.</para>
/// </summary>
public class RefreshingCookieFactory : CookieValidationFactory
{
    /// <summary>Comfortably past the 30-minute default, and nothing else reads this clock.</summary>
    private static readonly TimeSpan PastTheValidationInterval = TimeSpan.FromMinutes(31);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Runs after the application's own registrations, and PostConfigureSecurityStampValidatorOptions
        // only fills TimeProvider when it is still null, so this value is the one that survives.
        builder.ConfigureServices(services =>
            services.Configure<SecurityStampValidatorOptions>(
                options => options.TimeProvider = new OffsetTimeProvider(PastTheValidationInterval)));
    }

    private sealed class OffsetTimeProvider(TimeSpan offset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + offset;
    }
}

/// <summary>
/// Signing a real user in through the real login form, shared by the two cookie-validation test
/// classes so they cannot drift apart on how a session is established.
/// </summary>
internal static class CookieTestSession
{
    /// <summary>
    /// The raw, unquoted statement the handler emits via <c>ExecuteSqlInterpolatedAsync</c>. EF Core
    /// quotes identifiers, so nothing else in the application collides with it.
    /// </summary>
    internal const string LastLoginWrite = "UPDATE AspNetUsers";

    internal const string IdentityCookie = ".AspNetCore.Identity.Application";

    internal static bool DeletesIdentityCookie(HttpResponseMessage response) =>
        SetCookies(response).Any(c => c.StartsWith($"{IdentityCookie}=;", StringComparison.Ordinal));

    /// <summary>A renewal writes the cookie back with a value; a rejection writes it back empty.</summary>
    internal static bool RenewsIdentityCookie(HttpResponseMessage response) =>
        SetCookies(response).Any(c =>
            c.StartsWith($"{IdentityCookie}=", StringComparison.Ordinal) &&
            !c.StartsWith($"{IdentityCookie}=;", StringComparison.Ordinal));

    private static IEnumerable<string> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [];

    /// <summary>
    /// Creates a confirmed user and signs it in through the real login form, antiforgery token and
    /// all. <paramref name="clientIp"/> gives each test its own login rate-limiting partition.
    /// </summary>
    internal static async Task<(HttpClient Client, ApplicationUser User)> SignInAsync(
        TestWebApplicationFactory factory, string userName, string clientIp, string? role = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, clientIp);

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@test.com",
            EmailConfirmed = true,
            FirstName = "Cookie",
            LastName = "Validation",
            Nickname = userName,
            PhoneNumber = "123456789"
        };
        var password = TestSecret.NewPassword();

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
            if (role is not null)
            {
                (await userManager.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();
            }
        }

        var loginPage = await client.GetAsync("/login");
        var token = AntiforgeryFormToken.Find(await loginPage.Content.ReadAsStringAsync());
        token.Should().NotBeNullOrEmpty();

        var loginResponse = await client.PostAsync("/auth/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                [AntiforgeryFormToken.FieldName] = token!,
                ["Username"] = userName,
                ["Password"] = password,
                ["RememberMe"] = "false"
            }));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        return (client, user);
    }
}
