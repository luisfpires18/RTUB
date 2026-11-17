using System.Data.Common;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Web.Extensions;

namespace RTUB.Integration.Tests;

/// <summary>
/// Characterises what the application cookie's <c>OnValidatePrincipal</c> handler
/// (<c>ServiceCollectionExtensions.AddCookieAuthenticationServices</c>) actually does, in two
/// halves that must be read together:
/// <list type="bullet">
/// <item>the <b>security</b> checks it performs — these must never regress, whatever is done about
/// the cost;</item>
/// <item>the <b>database cost</b> it pays per request — deliberately exact, so any change to the
/// handler has to restate them. Unit 019 throttled the <c>LastLoginDate</c> write to one per
/// <see cref="ServiceCollectionExtensions.ActivityWriteThrottle"/> per user; the security checks
/// were left running on every request.</item>
/// </list>
/// The cost assertions are pinned to the raw, unquoted <c>UPDATE AspNetUsers</c> the handler emits
/// via <c>ExecuteSqlInterpolatedAsync</c>; EF Core quotes identifiers (<c>UPDATE "AspNetUsers"</c>),
/// so nothing else in the application can be mistaken for it.
/// <para>Unit 020 added a call to Identity's own <c>SecurityStampValidator</c> after these checks.
/// Inside its <c>ValidationInterval</c> that call does nothing at all, which is why every count
/// below is unchanged; <see cref="IdentityCookieRefreshTests"/> covers the requests on which it
/// does something.</para>
/// </summary>
public partial class CookieValidationTests : IClassFixture<CookieValidationFactory>
{
    private const string LastLoginWrite = CookieTestSession.LastLoginWrite;
    private const string RoleLookup = "\"AspNetRoles\"";
    private const string UserRoleLookup = "\"AspNetUserRoles\"";

    private readonly CookieValidationFactory _factory;

    public CookieValidationTests(CookieValidationFactory factory)
    {
        _factory = factory;
    }

    // ---------------------------------------------------------------- database cost (unit 019)

    [Fact]
    public async Task ValidCookie_IsAccepted_AndFirstRequestCostsOneWrite_AndNoRoleReadWithoutTheAdminClaim()
    {
        var (client, _) = await SignInNewUserAsync("cost-single", "10.30.0.1");

        using (_factory.Sql.Recording())
        {
            var response = await client.GetAsync("/Events");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            DeletesIdentityCookie(response).Should().BeFalse("a valid cookie must not be rejected");
        }

        _factory.Sql.Count(LastLoginWrite).Should().Be(1,
            "the first request of a session writes LastLoginDate and arms the throttle");
        _factory.Sql.Count(RoleLookup).Should().Be(0,
            "since task 054 the Admin probe (role by normalized name, then the join table) runs only for a cookie " +
            "that claims Admin; this one does not, so the stamp check is the only read");
        _factory.Sql.Count(UserRoleLookup).Should().Be(0);
    }

    [Fact]
    public async Task WithinTheValidationInterval_TheFrameworkValidatorCostsNothing_AndDoesNotRenew()
    {
        var (client, _) = await SignInNewUserAsync("cost-norenew", "10.30.0.11");

        var response = await client.GetAsync("/Events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CookieTestSession.RenewsIdentityCookie(response).Should().BeFalse(
            "SecurityStampValidator.ValidateAsync returns without touching the principal until " +
            "SecurityStampValidatorOptions.ValidationInterval has elapsed — 30 minutes by default");

        // The exact counts asserted by the tests around this one are the rest of the proof: unit
        // 020 added a framework call to the handler and not one extra database command inside the
        // interval. IdentityCookieRefreshTests covers the requests where the refresh is due.
    }

    [Fact]
    public async Task RepeatedAuthenticatedRequests_WriteLastLoginDate_OnlyOncePerThrottleWindow()
    {
        var (client, user) = await SignInNewUserAsync("cost-repeat", "10.30.0.2");

        using (_factory.Sql.Recording())
        {
            for (var i = 0; i < 5; i++)
            {
                var response = await client.GetAsync("/Events");

                // Throttling the write must not cost the session: every one of these is still a
                // fully validated, still-authenticated request.
                response.StatusCode.Should().Be(HttpStatusCode.OK);
                DeletesIdentityCookie(response).Should().BeFalse();
            }
        }

        _factory.Sql.Count(LastLoginWrite).Should().Be(1,
            "the first request writes and arms the throttle; the other four skip the UPDATE");
        _factory.Sql.Count(RoleLookup).Should().Be(0,
            "the throttle covers the activity write only — the stamp check still runs on every request, and the " +
            "Admin probe only for a cookie that claims Admin (task 054), which this one does not");
        _factory.Sql.Count(UserRoleLookup).Should().Be(0);

        // The value still moved: activity semantics are preserved, just written less often.
        (await ReadLastLoginDateAsync(user.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task AdminCookie_StillProbesTheRole_OnEveryRequest()
    {
        var (client, _) = await SignInNewUserAsync("cost-admin", "10.30.0.12", role: "Admin");

        using (_factory.Sql.Recording())
        {
            for (var i = 0; i < 5; i++)
            {
                (await client.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }

        // A cookie that claims Admin is the one a revoked Admin would still hold, so it is checked against the
        // database on every request, as before task 054: the role by normalized name, then the join table.
        _factory.Sql.Count(RoleLookup).Should().Be(5,
            "UserManager.IsInRoleAsync resolves the Admin role by normalized name on every request");
        _factory.Sql.Count(UserRoleLookup).Should().Be(5,
            "UserManager.IsInRoleAsync then probes the join table on every request");
    }

    [Fact]
    public async Task ThrottleIsKeyedPerUser_NotShared()
    {
        var (clientA, _) = await SignInNewUserAsync("throttle-a", "10.30.0.9");
        var (clientB, _) = await SignInNewUserAsync("throttle-b", "10.30.0.10");

        using (_factory.Sql.Recording())
        {
            (await clientA.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await clientB.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await clientA.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await clientB.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        _factory.Sql.Count(LastLoginWrite).Should().Be(2,
            "one write each — one user's activity write must never suppress another's");
    }

    [Fact]
    public void ActivityWriteThrottle_IsFiveMinutes()
    {
        // Pinned rather than exercised: proving real expiry would need a controllable cache clock,
        // which is disproportionate here. The cache-hit path is covered by the tests above.
        ServiceCollectionExtensions.ActivityWriteThrottle.Should().Be(TimeSpan.FromMinutes(5));
        ServiceCollectionExtensions.ActivityWriteCachePrefix.Should().Be("activity-lastlogin:");
    }

    [Fact]
    public async Task StaticFileRequest_SkipsCookieValidation()
    {
        var (client, _) = await SignInNewUserAsync("cost-static", "10.30.0.3");

        using (_factory.Sql.Recording())
        {
            (await client.GetAsync("/service-worker.js")).StatusCode.Should().Be(HttpStatusCode.OK);
            // /images/* is an ordinary static file since task 054 (the ImagesController that served it after
            // authentication is gone).
            (await client.GetAsync("/images/default-avatar.webp")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        _factory.Sql.Count(LastLoginWrite).Should().Be(0,
            "UseStaticFiles is registered before UseAuthentication, so static assets are free");
    }

    [Fact]
    public async Task MissingImageRequest_StillReachesCookieValidation()
    {
        var (client, _) = await SignInNewUserAsync("cost-image", "10.30.0.4");

        using (_factory.Sql.Recording())
        {
            // A missing /images/* file is nothing for UseStaticFiles to answer, so the request runs the rest of the
            // pipeline — authentication included — and 404s at the end of it. (An existing one is answered before
            // authentication: StaticFileRequest_SkipsCookieValidation.)
            (await client.GetAsync("/images/does-not-exist.png")).StatusCode
                .Should().Be(HttpStatusCode.NotFound);
        }

        _factory.Sql.Count(LastLoginWrite).Should().Be(1,
            "a request that reaches the endpoint pipeline, even a 404, pays for a cookie validation");
    }

    [Fact]
    public async Task Login_SetsLastLoginDate()
    {
        var (_, user) = await SignInNewUserAsync("login-stamps", "10.30.0.8");

        (await ReadLastLoginDateAsync(user.Id)).Should().NotBeNull(
            "the /auth/login endpoint writes LastLoginDate before issuing the cookie");
    }

    // ------------------------------------------------------------------------------- helpers

    /// <summary>
    /// A rejected cookie is observable twice over: the rejecting response deletes the cookie, and
    /// the request after it is anonymous, so it returns before any database work. The deletion is
    /// the decisive signal — with the activity throttle armed, the follow-up request would do no
    /// write in either case.
    /// </summary>
    private async Task AssertCookieRejectedAsync(HttpClient client)
    {
        var rejecting = await client.GetAsync("/Events");
        DeletesIdentityCookie(rejecting).Should().BeTrue("rejection signs the principal out");

        using (_factory.Sql.Recording())
        {
            await client.GetAsync("/Events");
        }

        _factory.Sql.Count(LastLoginWrite).Should().Be(0,
            "the client is anonymous now, so cookie validation returns before any database work");
    }

    private static bool DeletesIdentityCookie(HttpResponseMessage response) =>
        CookieTestSession.DeletesIdentityCookie(response);

    private async Task<DateTime?> ReadLastLoginDateAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.LastLoginDate)
            .SingleAsync();
    }

    private Task<(HttpClient Client, ApplicationUser User)> SignInNewUserAsync(
        string userName, string clientIp, string? role = null) =>
        CookieTestSession.SignInAsync(_factory, userName, clientIp, role);
}
