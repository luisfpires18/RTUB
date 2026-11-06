using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/pending-actions and /pending-actions (task 071) through the real host: real login, antiforgery and SQLite (so
/// the panel queries are proven to translate). Other tests share this database, so these assert on their own rows,
/// never on totals that other tests could change.
/// </summary>
public class PendingActionsApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;
    private static int _ip;
    private static int _day = 4000;

    public PendingActionsApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ThePageAndTheApi_AreForSignedInMembers()
    {
        var anonymous = Anonymous();

        (await anonymous.GetAsync("/api/pending-actions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/pending-actions/panels/events")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var page = await anonymous.GetAsync("/pending-actions");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=%2Fpending-actions");

        var (member, _) = await SignInAsync([MemberCategory.Leitao], retired: false);
        var shell = await member.GetAsync("/pending-actions");
        shell.StatusCode.Should().Be(HttpStatusCode.OK);
        shell.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        var api = await member.GetAsync("/api/pending-actions");
        api.Headers.CacheControl!.NoStore.Should().BeTrue("private data is never cached");
    }

    [Fact]
    public async Task AnActiveMember_SeesTheirUnansweredActivities_InCompactPagedPanels()
    {
        var (member, me) = await SignInAsync([MemberCategory.Tuno], retired: false);
        var mine = await AddEventAsync(NextDay());
        var answered = await AddEventAsync(NextDay());
        var rehearsal = await AddRehearsalAsync(NextDay());
        await AnswerEventAsync(answered, me.Id);

        var page = await member.GetFromJsonAsync<JsonElement>("/api/pending-actions");
        var raw = await member.GetStringAsync("/api/pending-actions/panels/events?filter=upcoming&page=1");

        page.GetProperty("status").GetString().Should().Be("active");
        page.GetProperty("panels").EnumerateArray().Select(p => p.GetProperty("key").GetString()).Should().Equal("events", "rehearsals");
        raw.Should().NotContain(me.Id).And.NotContain("@test.com");
        var keys = await AllKeysAsync(member, "events", "upcoming");
        keys.Should().Contain(mine.ToString()).And.NotContain(answered.ToString());
        (await AllKeysAsync(member, "rehearsals", "upcoming")).Should().Contain(rehearsal.ToString());
        var panel = JsonDocument.Parse(raw).RootElement;
        panel.GetProperty("pageSize").GetInt32().Should().Be(3);
        panel.GetProperty("rows").GetArrayLength().Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task AReformado_GetsThePage_ButNoParticipationPanels()
    {
        var (member, _) = await SignInAsync([MemberCategory.Tuno], retired: true);

        var page = await member.GetFromJsonAsync<JsonElement>("/api/pending-actions");

        page.GetProperty("status").GetString().Should().Be("retired");
        page.GetProperty("panels").GetArrayLength().Should().Be(0);
        (await member.GetAsync("/api/pending-actions/panels/events")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DismissingAnAnswer_IsRefused_AndNeedsTheAntiforgeryToken()
    {
        var (member, _) = await SignInAsync([MemberCategory.Tuno], retired: false);
        var id = await AddEventAsync(NextDay());
        var body = new { kind = "event.response", sourceKey = id.ToString() };

        (await member.PostAsJsonAsync("/api/pending-actions/dismissals", body)).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "no X-CSRF-TOKEN");
        await WithTokenAsync(member);
        (await member.PostAsJsonAsync("/api/pending-actions/dismissals", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "an answer is given, not dismissed, whatever the request claims");
        (await member.PostAsJsonAsync("/api/pending-actions/dismissals", new { kind = "event.response", sourceKey = "999999" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PendingActionDismissals.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Login_LandsAnActiveMemberOnTheirActions_UnlessTheyAskedForAPage()
    {
        var (active, user) = await NewUserAsync([MemberCategory.Leitao], retired: false);
        var (retired, other) = await NewUserAsync([MemberCategory.Tuno], retired: true);

        (await LoginAsync(active, user, returnUrl: null)).Should().Be("/pending-actions");
        (await LoginAsync(Client(), user, returnUrl: "/logistics/3?card=7")).Should().Be("/logistics/3?card=7", "a deep link wins");
        (await LoginAsync(Client(), user, returnUrl: "https://evil.example/")).Should().Be("/pending-actions");
        (await LoginAsync(retired, other, returnUrl: null)).Should().Be("/events");

        var again = await active.GetAsync("/login");
        again.Headers.Location!.ToString().Should().Be("/pending-actions", "a signed-in active member opening /login goes to the same place");
    }

    // ---------- helpers ----------

    private static int NextDay() => Interlocked.Increment(ref _day);

    private static async Task<List<string>> AllKeysAsync(HttpClient client, string panel, string filter)
    {
        var keys = new List<string>();
        for (var page = 1; ; page++)
        {
            var body = await client.GetFromJsonAsync<JsonElement>($"/api/pending-actions/panels/{panel}?filter={filter}&page={page}");
            keys.AddRange(body.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("sourceKey").GetString()!));
            var total = body.GetProperty("total").GetInt32();
            if (body.GetProperty("page").GetInt32() * 3 >= total)
            {
                return keys;
            }
        }
    }

    private async Task<int> AddEventAsync(int days)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var e = Event.Create($"Ação {days}", PortugalTime.Today(TimeProvider.System).AddDays(days).AddHours(21), "Braga", Enum.GetValues<EventType>()[0]);
        db.Events.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    private async Task<int> AddRehearsalAsync(int days)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var r = Rehearsal.Create(PortugalTime.Today(TimeProvider.System).AddDays(days), "Centro Académico");
        db.Rehearsals.Add(r);
        await db.SaveChangesAsync();
        return r.Id;
    }

    private async Task AnswerEventAsync(int eventId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Enrollments.Add(Enrollment.Create(userId, eventId));
        await db.SaveChangesAsync();
    }

    private async Task SetMembershipAsync(string userId, List<MemberCategory> categories, bool retired)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.Categories = categories;
        user.IsRetired = retired;
        await db.SaveChangesAsync();
    }

    private async Task<(HttpClient Client, ApplicationUser User)> SignInAsync(List<MemberCategory> categories, bool retired)
    {
        var n = Interlocked.Increment(ref _ip);
        var (client, user) = await CookieTestSession.SignInAsync(_factory, $"pa{Guid.NewGuid():N}"[..20], $"10.81.{n / 250}.{n % 250 + 1}");
        await SetMembershipAsync(user.Id, categories, retired);
        return (client, user);
    }

    /// <summary>A confirmed account (not signed in yet) with the given membership, and a client of its own.</summary>
    private async Task<(HttpClient Client, (string UserName, string Password) Login)> NewUserAsync(List<MemberCategory> categories, bool retired)
    {
        var name = $"pl{Guid.NewGuid():N}"[..20];
        var password = TestSecret.NewPassword();
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = name, Email = $"{name}@test.com", EmailConfirmed = true, FirstName = "Ana", LastName = "Silva", Nickname = name,
                Categories = categories, IsRetired = retired,
            };
            (await users.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        }

        return (Client(), (name, password));
    }

    private static async Task<string> LoginAsync(HttpClient client, (string UserName, string Password) login, string? returnUrl)
    {
        var token = await AntiforgeryFormToken.FetchAsync(client);
        var form = new Dictionary<string, string>
        {
            [AntiforgeryFormToken.FieldName] = token!,
            ["Username"] = login.UserName,
            ["Password"] = login.Password,
            ["RememberMe"] = "false",
        };
        if (returnUrl is not null)
        {
            form["ReturnUrl"] = returnUrl;
        }

        var response = await client.PostAsync("/auth/login", new FormUrlEncodedContent(form));
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        return response.Headers.Location!.ToString();
    }

    private HttpClient Client()
    {
        var n = Interlocked.Increment(ref _ip);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, $"10.82.{n / 250}.{n % 250 + 1}");
        return client;
    }

    private HttpClient Anonymous() => Client();

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
