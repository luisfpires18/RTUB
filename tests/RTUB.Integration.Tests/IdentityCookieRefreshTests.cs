using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Core.Entities;

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
public partial class IdentityCookieRefreshTests : IClassFixture<RefreshingCookieFactory>
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

        var denied = await GetOwnerOnlyAsync(client);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the session is authenticated, so the Owner gate forbids rather than challenges");

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
        var allowed = await GetOwnerOnlyAsync(client);

        allowed.StatusCode.Should().Be(HttpStatusCode.OK,
            "reaching the Owner-only read at all proves the Owner role claim is now on the principal");
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

        // Inside the interval this request costs no role read (CookieValidationTests pins that: since task 054
        // RTUB's Admin probe runs only for a cookie that claims Admin). The refresh adds
        // CreateUserPrincipalAsync's own reads: one role join and one user-claims SELECT — two
        // commands, once per user per ValidationInterval.
        _factory.Sql.Count(RoleLookup).Should().Be(1,
            "the role join CreateUserPrincipalAsync runs; RTUB's Admin probe is skipped for a cookie without the claim");
        _factory.Sql.Count(UserRoleLookup).Should().Be(1);
        _factory.Sql.Count(UserClaimLookup).Should().Be(1,
            "the rebuilt principal also picks up the user's own claims");

        // Two, not three: the framework's own VerifySecurityStamp repeats RTUB's stamp read but
        // adds no SELECT for it, because UserManager.GetUserAsync resolves off the request-scoped
        // DbContext's change tracker that RTUB's check has already populated.
    }

    // An Owner-only read (OwnerEndpoints' gate, task 035): 403 for any other member, 200 for the Owner, and it changes
    // nothing. (Until 038 this probe was the old PushController broadcast, now removed.)
    private static Task<HttpResponseMessage> GetOwnerOnlyAsync(HttpClient client) => client.GetAsync("/api/owner/db/tables");
}
