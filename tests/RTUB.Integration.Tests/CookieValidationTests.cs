using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Web.Extensions;
using Xunit;

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
public class CookieValidationTests : IClassFixture<CookieValidationFactory>
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
    public async Task ValidCookie_IsAccepted_AndFirstRequestCostsOneWritePlusTwoRoleReads()
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
        _factory.Sql.Count(RoleLookup).Should().Be(1,
            "UserManager.IsInRoleAsync resolves the Admin role by normalized name");
        _factory.Sql.Count(UserRoleLookup).Should().Be(1,
            "UserManager.IsInRoleAsync then probes the join table");
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
        _factory.Sql.Count(RoleLookup).Should().Be(5,
            "the throttle covers the activity write only — security runs on every request");
        _factory.Sql.Count(UserRoleLookup).Should().Be(5);

        // The value still moved: activity semantics are preserved, just written less often.
        (await ReadLastLoginDateAsync(user.Id)).Should().NotBeNull();
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
        }

        _factory.Sql.Count(LastLoginWrite).Should().Be(0,
            "UseStaticFiles is registered before UseAuthentication, so static assets are free");
    }

    [Fact]
    public async Task ImageControllerRequest_CostsAFullCookieValidation_EvenWhenTheImageIsMissing()
    {
        var (client, _) = await SignInNewUserAsync("cost-image", "10.30.0.4");

        using (_factory.Sql.Recording())
        {
            // /images/* is deliberately excluded from UseStaticFiles (ImagesController adds ETags),
            // so it runs the full pipeline — authentication included.
            (await client.GetAsync("/images/does-not-exist.png")).StatusCode
                .Should().Be(HttpStatusCode.NotFound);
        }

        _factory.Sql.Count(LastLoginWrite).Should().Be(1,
            "every image request, even a 404, pays for a cookie validation");
    }

    // ------------------------------------------------------------------------ security checks

    [Fact]
    public async Task SecurityStampChange_RejectsCookie()
    {
        var (client, user) = await SignInNewUserAsync("sec-stamp", "10.30.0.5");

        // Arm the activity-write throttle first: the security checks must still reject on a
        // request that would skip the LastLoginDate write.
        (await client.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(user.Id);
            (await userManager.UpdateSecurityStampAsync(stored!)).Succeeded.Should().BeTrue();
        }

        await AssertCookieRejectedAsync(client);
    }

    [Fact]
    public async Task ExpelledUser_RejectsCookie()
    {
        var (client, user) = await SignInNewUserAsync("sec-expelled", "10.30.0.6");

        // Arm the activity-write throttle first: the security checks must still reject on a
        // request that would skip the LastLoginDate write.
        (await client.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(user.Id);
            stored!.IsExpelled = true;
            // UpdateAsync does not touch the security stamp, so this exercises the expulsion
            // branch specifically and not the stamp branch above it.
            (await userManager.UpdateAsync(stored)).Succeeded.Should().BeTrue();
        }

        await AssertCookieRejectedAsync(client);
    }

    [Fact]
    public async Task AdminRoleRemoved_RejectsCookie()
    {
        var (client, user) = await SignInNewUserAsync("sec-admin", "10.30.0.7", role: "Admin");

        // Arm the activity-write throttle first: the security checks must still reject on a
        // request that would skip the LastLoginDate write.
        (await client.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(user.Id);
            // RemoveFromRoleAsync leaves the security stamp alone, so the stock
            // SecurityStampValidator would NOT catch this — only RTUB's own role probe does.
            (await userManager.RemoveFromRoleAsync(stored!, "Admin")).Succeeded.Should().BeTrue();
        }

        await AssertCookieRejectedAsync(client);
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

/// <summary>
/// The shared factory plus a recorder for the SQL its <see cref="ApplicationDbContext"/>s execute.
/// The context is re-registered against <see cref="TestWebApplicationFactory.ConnectionString"/> —
/// the same database — purely so the interceptor can be attached.
/// </summary>
public class CookieValidationFactory : TestWebApplicationFactory
{
    public SqlRecorder Sql { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(ConnectionString);
                options.EnableSensitiveDataLogging();
                options.AddInterceptors(new RecordingCommandInterceptor(Sql));
            });
        });
    }
}

/// <summary>
/// Records executed SQL, but only between <see cref="Recording"/> and the disposal of its handle,
/// so the application's background services cannot colour a measurement taken around one request.
/// </summary>
public sealed class SqlRecorder
{
    private readonly ConcurrentQueue<string> _statements = new();
    private volatile bool _enabled;

    public IDisposable Recording()
    {
        _statements.Clear();
        _enabled = true;
        return new Handle(this);
    }

    internal void Add(string sql)
    {
        if (_enabled)
        {
            _statements.Enqueue(sql);
        }
    }

    public int Count(string fragment) =>
        _statements.Count(s => s.Contains(fragment, StringComparison.Ordinal));

    private sealed class Handle : IDisposable
    {
        private readonly SqlRecorder _recorder;
        public Handle(SqlRecorder recorder) => _recorder = recorder;
        public void Dispose() => _recorder._enabled = false;
    }
}

public sealed class RecordingCommandInterceptor : DbCommandInterceptor
{
    private readonly SqlRecorder _recorder;

    public RecordingCommandInterceptor(SqlRecorder recorder) => _recorder = recorder;

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        _recorder.Add(command.CommandText);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        _recorder.Add(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        _recorder.Add(command.CommandText);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _recorder.Add(command.CommandText);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        _recorder.Add(command.CommandText);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }
}
