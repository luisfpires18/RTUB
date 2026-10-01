using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/events (React track 011) through the real host: the real login, real antiforgery, the real
/// SQLite test database and the real EnrollmentService. Push is a recording fake, so nothing is
/// ever sent; video and image links are fake https URLs and storage is never called.
/// </summary>
public class EventsApiTests : IClassFixture<EventsApiFactory>
{
    private const string Description = "Descrição só para membros";
    private const string Reason = "Motivo interno do cancelamento";
    private const string MemberNote = "nota-privada-de-um-membro";

    private readonly EventsApiFactory _factory;

    public EventsApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    // ---------- reading ----------

    [Fact]
    public async Task AVisitor_GetsThePublicAgenda_AndNothingPrivate()
    {
        var seed = await SeedAsync();

        var response = await Anonymous().GetAsync("/api/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue("the answer depends on the session");
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain(Description).And.NotContain(Reason).And.NotContain(MemberNote)
            .And.NotContain(seed.MemberId, "no user id ever reaches a visitor")
            .And.NotContainAny(new[] { "userId", "email", "notes", "enrollments", "createdBy", "cancellationReason", "\"description\"" });

        var body = JsonDocument.Parse(raw).RootElement;
        body.GetProperty("isMember").GetBoolean().Should().BeFalse();
        var all = body.GetProperty("upcoming").EnumerateArray().Concat(body.GetProperty("past").EnumerateArray()).ToList();
        all.Should().OnlyContain(e => e.GetProperty("member").ValueKind == JsonValueKind.Null);

        var festival = all.Single(e => e.GetProperty("id").GetInt32() == seed.Festival);
        festival.GetProperty("trophies").EnumerateArray().Select(t => t.GetString()).Should().Equal("1.º Prémio", "Melhor Pandeireta");
        festival.GetProperty("videoCount").GetInt32().Should().Be(1);
        all.Single(e => e.GetProperty("id").GetInt32() == seed.Cancelled).GetProperty("cancelled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task TheAgenda_SplitsUpcomingAndPast_InTheirOrders()
    {
        var seed = await SeedAsync();

        var body = await Anonymous().GetFromJsonAsync<JsonElement>("/api/events");

        var upcoming = Ids(body.GetProperty("upcoming"));
        upcoming.Should().ContainInOrder(seed.MultiDay, seed.Open, seed.Cancelled);
        upcoming.Should().NotContain(new[] { seed.Festival, seed.Past });
        Ids(body.GetProperty("past")).Should().ContainInOrder(seed.Past, seed.Festival);
    }

    [Fact]
    public async Task OneEvent_ForAVisitor_HasItsVideosButNoMemberSection()
    {
        var seed = await SeedAsync();

        var response = await Anonymous().GetAsync($"/api/events/{seed.Festival}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("member").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("videos").EnumerateArray().Single().GetProperty("url").GetString().Should().StartWith("https://pub-test.r2.dev/");
    }

    [Fact]
    public async Task AnUnknownEvent_Is404_ForEveryone()
    {
        await SeedAsync();
        var (member, _) = await SignInAsync();

        (await Anonymous().GetAsync("/api/events/987654")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.GetAsync("/api/events/987654")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.GetAsync("/api/events/987654/attendance")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ASignedInMember_GetsTheMemberView_WithTheReasonAndTheirOwnAnswerOnly()
    {
        var seed = await SeedAsync();
        var (member, _) = await SignInAsync();

        var cancelled = await member.GetFromJsonAsync<JsonElement>($"/api/events/{seed.Cancelled}");
        var open = await member.GetFromJsonAsync<JsonElement>($"/api/events/{seed.Open}");

        cancelled.GetProperty("isMember").GetBoolean().Should().BeTrue();
        cancelled.GetProperty("member").GetProperty("cancellationReason").GetString().Should().Be(Reason);
        open.GetProperty("event").GetProperty("member").GetProperty("description").GetString().Should().Be(Description);
        open.GetProperty("event").GetProperty("member").GetProperty("myStatus").ValueKind.Should().Be(JsonValueKind.Null,
            "someone else's answer is never this member's");
        open.GetProperty("event").GetProperty("member").GetProperty("goingCount").GetInt32().Should().Be(1);
        open.ToString().Should().NotContain(MemberNote).And.NotContain(seed.MemberId);
    }

    // ---------- attendance ----------

    [Fact]
    public async Task Attendance_IsForSignedInMembersOnly()
    {
        var seed = await SeedAsync();
        var visitor = Anonymous();
        await WithTokenAsync(visitor);

        (await visitor.GetAsync($"/api/events/{seed.Open}/attendance")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await visitor.PutAsJsonAsync($"/api/events/{seed.Open}/attendance", Answer(true))).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await visitor.DeleteAsync($"/api/events/{seed.Past}/attendance")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Writes_WithoutTheAntiforgeryHeader_AreRefused_AndChangeNothing()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync();

        var put = await member.PutAsJsonAsync($"/api/events/{seed.Open}/attendance", Answer(true));
        var delete = await member.DeleteAsync($"/api/events/{seed.Past}/attendance");
        var play = await member.PostAsync($"/api/events/videos/{seed.Video}/plays", null);

        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        play.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await EnrollmentAsync(seed.Open, user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AMember_AnswersGoing_ThenNotGoing_OnTheSameEnrollment_WithNotificationsFaked()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync();
        await WithTokenAsync(member);

        var going = await member.PutAsJsonAsync($"/api/events/{seed.Open}/attendance", Answer(true, notes: "levo capa"));
        going.StatusCode.Should().Be(HttpStatusCode.OK);
        (await going.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("going");

        var notGoing = await member.PutAsJsonAsync($"/api/events/{seed.Open}/attendance", Answer(false, notes: "doente"));
        notGoing.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await EnrollmentAsync(seed.Open, user.Id);
        stored!.WillAttend.Should().BeFalse();
        stored.Notes.Should().Be("doente");
        (await CountEnrollmentsAsync(seed.Open, user.Id)).Should().Be(1, "a second answer updates, never duplicates");
    }

    [Fact]
    public async Task AnInvalidInstrument_IsAFieldError()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync();
        await WithTokenAsync(member);

        var response = await member.PutAsJsonAsync($"/api/events/{seed.Open}/attendance", Answer(true, instrument: "Violino"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("instrument", out _).Should().BeTrue();
        (await EnrollmentAsync(seed.Open, user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task CancelledAndPastEvents_TakeNoNewAnswer()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync();
        await WithTokenAsync(member);

        (await member.PutAsJsonAsync($"/api/events/{seed.Cancelled}/attendance", Answer(true))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await member.PutAsJsonAsync($"/api/events/{seed.Past}/attendance", Answer(true))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await EnrollmentAsync(seed.Cancelled, user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task APastEvent_LetsTheMemberWhoWentWithdraw()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync();
        await AddEnrollmentAsync(seed.Past, user.Id, willAttend: true);
        await WithTokenAsync(member);

        var response = await member.DeleteAsync($"/api/events/{seed.Past}/attendance");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnrollmentAsync(seed.Past, user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AVideoPlay_IsRecorded_ForVisitorsToo()
    {
        var seed = await SeedAsync();
        var visitor = Anonymous();
        await WithTokenAsync(visitor);

        (await visitor.PostAsync($"/api/events/videos/{seed.Video}/plays", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await visitor.PostAsync("/api/events/videos/987654/plays", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheHomePreview_StillGivesAtMostThreeUpcomingEvents_WithoutIds()
    {
        await SeedAsync();

        var raw = await Anonymous().GetStringAsync("/api/public/events/upcoming");

        JsonDocument.Parse(raw).RootElement.GetArrayLength().Should().BeLessThanOrEqualTo(3);
        raw.Should().NotContain("\"id\"").And.NotContain(Description).And.NotContain(Reason);
    }

    // ---------- helpers ----------

    private sealed record Seed(int Open, int MultiDay, int Cancelled, int Past, int Festival, int Video, string MemberId);

    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static Seed? _seed;

    /// <summary>One set of events per test class; every test reads it and writes only its own member's answers.</summary>
    private async Task<Seed> SeedAsync()
    {
        await Lock.WaitAsync();
        try
        {
            if (_seed is not null)
            {
                return _seed;
            }

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var today = DateTime.Today;

            var other = await db.Users.Select(u => u.Id).FirstAsync();
            Event Add(string name, DateTime date, DateTime? end = null, EventType type = EventType.Atuacao, string description = "")
            {
                var e = Event.Create(name, date, "Bragança", type, description);
                e.EndDate = end;
                db.Events.Add(e);
                return e;
            }

            var open = Add("Serenata aberta", today.AddDays(1000).AddHours(21), description: Description);
            var multiDay = Add("Festival de três dias", today.AddDays(999), end: today.AddDays(1001));
            var cancelled = Add("Arraial cancelado", today.AddDays(1002));
            cancelled.Cancel(Reason);
            var past = Add("Atuação passada", today.AddDays(-1000));
            var festival = Add("Festival antigo", today.AddDays(-1001), type: EventType.Festival);
            await db.SaveChangesAsync();

            db.Trophies.Add(Trophy.Create("Melhor Pandeireta", festival.Id));
            db.Trophies.Add(Trophy.Create("1.º Prémio", festival.Id));
            var video = EventVideo.CreateVideo(festival.Id, "https://pub-test.r2.dev/videos/events/festival.mp4", "video/mp4", 10, other, "Atuação");
            db.EventVideos.Add(video);
            var note = Enrollment.Create(other, open.Id);
            note.Notes = MemberNote;
            db.Enrollments.Add(note);
            await db.SaveChangesAsync();

            return _seed = new Seed(open.Id, multiDay.Id, cancelled.Id, past.Id, festival.Id, video.Id, other);
        }
        finally
        {
            Lock.Release();
        }
    }

    private static object Answer(bool willAttend, string? instrument = null, string? notes = null) => new { willAttend, instrument, notes };

    private static List<int> Ids(JsonElement list) => list.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync()
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"events-{Guid.NewGuid():N}"[..24], $"10.71.{n / 250}.{n % 250 + 1}");
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.72.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    private async Task<Enrollment?> EnrollmentAsync(int eventId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Enrollments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.UserId == userId);
    }

    private async Task<int> CountEnrollmentsAsync(int eventId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Enrollments
            .CountAsync(e => e.EventId == eventId && e.UserId == userId);
    }

    private async Task AddEnrollmentAsync(int eventId, string userId, bool willAttend)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var e = Enrollment.Create(userId, eventId);
        e.WillAttend = willAttend;
        db.Enrollments.Add(e);
        await db.SaveChangesAsync();
    }
}

/// <summary>The test host with push replaced by a do-nothing fake: an answer never notifies anyone for real.</summary>
public sealed class EventsApiFactory : TestWebApplicationFactory
{
    public Mock<IPushNotificationService> Push { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPushNotificationService>();
            services.AddSingleton(Push.Object);
        });
    }
}
