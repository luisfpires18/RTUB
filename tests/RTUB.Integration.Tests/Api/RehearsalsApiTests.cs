using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/rehearsals (React track 014) through the real host: real login, antiforgery and SQLite, the real
/// RehearsalService / RehearsalAttendanceService. Push is the recording fake of <see cref="EventsApiFactory"/>:
/// nothing is ever sent. Rehearsals speak of presença (attendance), never inscrição.
/// </summary>
public partial class RehearsalsApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;
    private static int _day = 2000;

    public RehearsalsApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>A day no other test uses (rehearsal dates are unique), this many days from today.</summary>
    private static int NextDay(bool past) => (past ? -1 : 1) * Interlocked.Increment(ref _day);

    // ---------- reading ----------

    [Fact]
    public async Task Rehearsals_AreForSignedInMembers_AndUnknownOnesAre404()
    {
        var id = await AddRehearsalAsync(NextDay(past: false));
        var (member, _) = await SignInAsync();

        (await Anonymous().GetAsync("/api/rehearsals")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Anonymous().GetAsync($"/api/rehearsals/{id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Anonymous().GetAsync("/api/rehearsals/stats")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await member.GetAsync("/api/rehearsals/987654")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var agenda = await member.GetFromJsonAsync<JsonElement>("/api/rehearsals");
        agenda.GetProperty("canManage").GetBoolean().Should().BeFalse();
        agenda.GetProperty("upcoming").EnumerateArray().Should().Contain(r => r.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task TheDetail_GroupsPresences_WithoutIdsEmailsOrPendingCountsForMembers()
    {
        var id = await AddRehearsalAsync(NextDay(past: true));
        var (member, me) = await SignInAsync();
        var (_, other) = await SignInAsync();
        await AddAttendanceAsync(id, other.Id, willAttend: true, attended: false, notes: "Chego tarde");
        await AddAttendanceAsync(id, me.Id, willAttend: false, attended: false);

        var response = await member.GetAsync($"/api/rehearsals/{id}");
        var raw = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        raw.Should().NotContain(other.Id).And.NotContain(me.Id).And.NotContain("@test.com").And.NotContainAny(new[] { "userId", "email", "phone" });
        var body = JsonDocument.Parse(raw).RootElement;
        body.GetProperty("rehearsal").GetProperty("pendingCount").GetInt32().Should().Be(0, "only Admin/Owner see pending counts");
        body.GetProperty("rehearsal").GetProperty("mine").GetProperty("status").GetString().Should().Be("notGoing");
        var going = body.GetProperty("attendance").GetProperty("going").EnumerateArray().Single();
        going.GetProperty("status").GetString().Should().Be("pending");
        going.GetProperty("notes").GetString().Should().Be("Chego tarde");
        going.GetProperty("canApprove").GetBoolean().Should().BeFalse();
        going.GetProperty("canRemove").GetBoolean().Should().BeFalse("a member removes only their own presença");
        body.GetProperty("attendance").GetProperty("notGoing").EnumerateArray().Single().GetProperty("canRemove").GetBoolean().Should().BeTrue();
    }

    // ---------- the member's own presença ----------

    [Fact]
    public async Task AMember_MarksAndChangesTheirPresenca_OnAnUpcomingRehearsal_WithPushThroughTheFake()
    {
        var id = await AddRehearsalAsync(NextDay(past: false));
        var (member, me) = await SignInAsync();
        await WithTokenAsync(member);

        var going = await member.PutAsJsonAsync($"/api/rehearsals/{id}/attendance", new { willAttend = true, instrument = (string?)null, notes = "Levo a viola" });
        going.StatusCode.Should().Be(HttpStatusCode.OK);
        (await going.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("pending", "a new presença waits for approval");
        var row = await AttendanceAsync(id, me.Id);
        row!.WillAttend.Should().BeTrue();
        row.Attended.Should().BeFalse();
        row.Notes.Should().Be("Levo a viola");

        var notGoing = await member.PutAsJsonAsync($"/api/rehearsals/{id}/attendance", new { willAttend = false, instrument = (string?)null, notes = "Exame" });
        (await notGoing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("notGoing");
        (await CountAttendancesAsync(id, me.Id)).Should().Be(1, "the same row changes");

        (await member.DeleteAsync($"/api/rehearsals/{id}/attendance")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AttendanceAsync(id, me.Id)).Should().BeNull();
    }

    [Fact]
    public async Task PresencaInput_IsValidated_AndPastOrCancelledRehearsalsTakeNone()
    {
        var upcoming = await AddRehearsalAsync(NextDay(past: false));
        var past = await AddRehearsalAsync(NextDay(past: true));
        var cancelled = await AddRehearsalAsync(NextDay(past: false), cancelled: true);
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);

        (await member.PutAsJsonAsync($"/api/rehearsals/{upcoming}/attendance", new { willAttend = true, instrument = "Trombone", notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "an instrument the page did not offer");
        (await member.PutAsJsonAsync($"/api/rehearsals/{upcoming}/attendance", new { willAttend = true, instrument = (string?)null, notes = new string('n', 501) }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.PutAsJsonAsync($"/api/rehearsals/{past}/attendance", new { willAttend = true, instrument = (string?)null, notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await member.PutAsJsonAsync($"/api/rehearsals/{cancelled}/attendance", new { willAttend = true, instrument = (string?)null, notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await member.PutAsJsonAsync("/api/rehearsals/987654/attendance", new { willAttend = true, instrument = (string?)null, notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Writes_WithoutTheAntiforgeryHeader_AreRefused_AndChangeNothing()
    {
        var id = await AddRehearsalAsync(NextDay(past: false));
        var (member, me) = await SignInAsync();
        var (admin, _) = await SignInAsync("Admin");

        (await member.PutAsJsonAsync($"/api/rehearsals/{id}/attendance", new { willAttend = true, instrument = (string?)null, notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.DeleteAsync($"/api/rehearsals/{id}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AttendanceAsync(id, me.Id)).Should().BeNull();
        (await RehearsalAsync(id)).Should().NotBeNull();
    }

    // ---------- helpers ----------

    private static object Input(string? date) => new { date, location = "Centro Académico", theme = "", description = "", notes = "" };

    private static string DateText(int offset) => DateText(DateTime.Today.AddDays(offset));

    private static string DateText(DateTime day) => day.ToString("yyyy-MM-dd");

    private Task<int> AddRehearsalAsync(int offset, bool cancelled = false) => AddRehearsalAtAsync(DateTime.Today.AddDays(offset), cancelled);

    private async Task<int> AddRehearsalAtAsync(DateTime day, bool cancelled = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var r = Rehearsal.Create(day, "Centro Académico");
        if (cancelled)
        {
            r.Cancel("Feriado");
        }

        db.Rehearsals.Add(r);
        await db.SaveChangesAsync();
        return r.Id;
    }

    private async Task<int> AddAttendanceAsync(int rehearsalId, string userId, bool willAttend, bool attended, string? notes = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var a = RehearsalAttendance.Create(rehearsalId, userId);
        a.WillAttend = willAttend;
        a.MarkAttendance(attended);
        a.Notes = notes;
        db.RehearsalAttendances.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }

    private async Task<Rehearsal?> RehearsalAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Rehearsals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    }

    private async Task<RehearsalAttendance?> AttendanceAsync(int rehearsalId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RehearsalAttendances.AsNoTracking()
            .FirstOrDefaultAsync(a => a.RehearsalId == rehearsalId && a.UserId == userId);
    }

    private async Task<int> CountAttendancesAsync(int rehearsalId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RehearsalAttendances
            .CountAsync(a => a.RehearsalId == rehearsalId && a.UserId == userId);
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"reh{Guid.NewGuid():N}"[..20], $"10.75.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.76.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
