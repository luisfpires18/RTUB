using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/rehearsals (React track 014) through the real host: real login, antiforgery and SQLite, the real
/// RehearsalService / RehearsalAttendanceService. Push is the recording fake of <see cref="EventsApiFactory"/>:
/// nothing is ever sent. Rehearsals speak of presença (attendance), never inscrição.
/// </summary>
public class RehearsalsApiTests : IClassFixture<EventsApiFactory>
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

    // ---------- Admin/Owner ----------

    [Theory]
    [InlineData(null)]
    [InlineData("Mod")]
    public async Task MembersAndMods_CannotManageRehearsals(string? role)
    {
        var id = await AddRehearsalAsync(NextDay(past: true));
        var (client, _) = await SignInAsync(role);
        var (_, other) = await SignInAsync();
        var row = await AddAttendanceAsync(id, other.Id, willAttend: true, attended: false);
        await WithTokenAsync(client);

        (await client.PostAsJsonAsync("/api/rehearsals", Input(DateText(NextDay(past: false))))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync($"/api/rehearsals/{id}", Input(null))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync($"/api/rehearsals/{id}/cancel", new { reason = "x" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync($"/api/rehearsals/{id}/attendances/{row}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.DeleteAsync($"/api/rehearsals/{id}/attendances/{row}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/rehearsals/{id}/notice")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.DeleteAsync($"/api/rehearsals/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await RehearsalAsync(id)).Should().NotBeNull();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminOrOwner_CreatesEditsCancelsReactivatesAndDeletes(string role)
    {
        var (admin, _) = await SignInAsync(role);
        await WithTokenAsync(admin);
        var date = DateText(NextDay(past: false));

        var created = await admin.PostAsJsonAsync("/api/rehearsals", new { date, location = "Centro Académico", theme = "Fados", description = "Repertório novo", notes = "Trazer pautas" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var saved = await RehearsalAsync(id);
        saved!.Description.Should().Be("Repertório novo", "the description typed when creating is kept now");
        saved.Notes.Should().Be("Trazer pautas");
        saved.StartTime.Should().Be(new TimeSpan(21, 30, 0));

        (await admin.PostAsJsonAsync("/api/rehearsals", new { date, location = "Outro", theme = "", description = "", notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "one rehearsal per date");
        (await admin.PostAsJsonAsync("/api/rehearsals", new { date = DateText(NextDay(past: false)), location = " ", theme = "", description = "", notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await admin.PutAsJsonAsync($"/api/rehearsals/{id}", new { date = (string?)null, location = "Nómada", theme = "", description = "", notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await RehearsalAsync(id))!.Location.Should().Be("Nómada");

        var (_, member) = await SignInAsync();
        await AddAttendanceAsync(id, member.Id, willAttend: true, attended: false);
        (await admin.PostAsJsonAsync($"/api/rehearsals/{id}/cancel", new { reason = " " })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync($"/api/rehearsals/{id}/cancel", new { reason = "Feriado" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RehearsalAsync(id))!.IsCanceled.Should().BeTrue();
        (await AttendanceAsync(id, member.Id)).Should().BeNull("cancelling removes the presenças, as before");

        (await admin.PostAsync($"/api/rehearsals/{id}/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RehearsalAsync(id))!.IsCanceled.Should().BeFalse();

        (await admin.DeleteAsync($"/api/rehearsals/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RehearsalAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task ARange_CreatesTuesdaysAndThursdays_SkipsTakenDates_AndIsLimited()
    {
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var monday = DateTime.Today.AddDays(5000 + ((8 - (int)DateTime.Today.AddDays(5000).DayOfWeek) % 7));
        await AddRehearsalAtAsync(monday.AddDays(1)); // that Tuesday is taken

        var response = await admin.PostAsJsonAsync("/api/rehearsals/range",
            new { from = DateText(monday), to = DateText(monday.AddDays(13)), location = "Centro Académico", theme = "", description = "" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("created").GetInt32().Should().Be(3, "two weeks hold 2 Tuesdays and 2 Thursdays, one taken");
        result.GetProperty("skipped").GetInt32().Should().Be(1);

        (await admin.PostAsJsonAsync("/api/rehearsals/range", new { from = DateText(monday), to = DateText(monday.AddDays(91)), location = "X", theme = "", description = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/rehearsals/range", new { from = DateText(DateTime.Today.AddDays(-1)), to = DateText(DateTime.Today), location = "X", theme = "", description = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task OnceApprovable_AdminOrOwner_ConfirmsRemovesAndAddsPresencas(string role)
    {
        var past = await AddRehearsalAsync(NextDay(past: true));
        var future = await AddRehearsalAsync(NextDay(past: false));
        var (admin, _) = await SignInAsync(role);
        var (_, pending) = await SignInAsync();
        var (_, extra) = await SignInAsync();
        var (_, other) = await SignInAsync();
        var row = await AddAttendanceAsync(past, pending.Id, willAttend: true, attended: false);
        var futureRow = await AddAttendanceAsync(future, pending.Id, willAttend: true, attended: false);
        var otherRow = await AddAttendanceAsync(past, other.Id, willAttend: false, attended: false);
        await WithTokenAsync(admin);

        (await admin.PostAsync($"/api/rehearsals/{future}/attendances/{futureRow}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "a future rehearsal cannot be approved yet");
        (await admin.PostAsync($"/api/rehearsals/{future}/attendances/{row}/approve", null)).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a presença only counts under its own rehearsal");
        (await admin.PostAsync($"/api/rehearsals/{past}/attendances/{row}/approve", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AttendanceAsync(past, pending.Id))!.Attended.Should().BeTrue();
        _factory.Push.Verify(p => p.SendToUserAsync(pending.Id, It.IsAny<SendPushNotificationDto>()), Times.Once, "the member hears it was confirmed, as before");

        var found = await admin.GetFromJsonAsync<JsonElement>($"/api/rehearsals/{past}/attendances/members?q={extra.UserName}");
        found.EnumerateArray().Should().ContainSingle(m => m.GetProperty("id").GetString() == extra.Id);
        (await admin.PostAsJsonAsync($"/api/rehearsals/{past}/attendances", new { userId = extra.Id })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var added = await AttendanceAsync(past, extra.Id);
        added!.Attended.Should().BeTrue("someone added afterwards was there");
        (await admin.PostAsJsonAsync($"/api/rehearsals/{past}/attendances", new { userId = extra.Id })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync($"/api/rehearsals/{future}/attendances", new { userId = extra.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await admin.DeleteAsync($"/api/rehearsals/{past}/attendances/{otherRow}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AttendanceAsync(past, other.Id)).Should().BeNull();
        (await admin.DeleteAsync($"/api/rehearsals/{future}/attendances/{futureRow}")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "before it can be approved, only the member removes their presença");
    }

    [Fact]
    public async Task Notices_GoToSubscribedMembers_ThroughTheFake_ForUpcomingRehearsalsOnly()
    {
        var upcoming = await AddRehearsalAsync(NextDay(past: false));
        var past = await AddRehearsalAsync(NextDay(past: true));
        var (admin, _) = await SignInAsync("Admin");
        var (_, subscriber) = await SignInAsync();
        _factory.Push.Setup(p => p.GetSubscribedUserIdsAsync()).ReturnsAsync(new[] { subscriber.Id });
        _factory.Push.Setup(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>())).ReturnsAsync((1, 0));
        await WithTokenAsync(admin);

        var audience = await admin.GetFromJsonAsync<JsonElement>($"/api/rehearsals/{upcoming}/notice");
        audience.GetProperty("subscribed").GetInt32().Should().Be(1);
        (await admin.PostAsJsonAsync($"/api/rehearsals/{upcoming}/notice", new { message = " ", onlyLeitoesAndCaloiros = false }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync($"/api/rehearsals/{past}/notice", new { message = "Hoje há ensaio", onlyLeitoesAndCaloiros = false }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var sent = await admin.PostAsJsonAsync($"/api/rehearsals/{upcoming}/notice", new { message = "Hoje há ensaio", onlyLeitoesAndCaloiros = false });
        sent.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Push.Verify(p => p.SendToSelectedUsersAsync(It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { subscriber.Id })),
            It.IsAny<SendPushNotificationDto>()), Times.Once);
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AuditLogs
            .AnyAsync(l => l.EntityType == "Rehearsal" && l.EntityId == upcoming && l.Action == "PushNotificationSent")).Should().BeTrue();
    }

    // ---------- statistics ----------

    [Fact]
    public async Task Stats_CountConfirmedAndPendingPresencas_AtPastNotCancelledRehearsals()
    {
        var days = new[] { -7000, -6999, -6998 };
        var done = await AddRehearsalAsync(days[0]);
        var waiting = await AddRehearsalAsync(days[1]);
        var cancelled = await AddRehearsalAsync(days[2], cancelled: true);
        var (member, me) = await SignInAsync();
        await AddAttendanceAsync(done, me.Id, willAttend: true, attended: true);
        await AddAttendanceAsync(waiting, me.Id, willAttend: true, attended: false);
        await AddAttendanceAsync(cancelled, me.Id, willAttend: true, attended: true);

        var stats = await member.GetFromJsonAsync<JsonElement>($"/api/rehearsals/stats?from={DateText(-7001)}&to={DateText(-6997)}");

        stats.GetProperty("pastRehearsals").GetInt32().Should().Be(2, "the cancelled one does not count");
        var row = stats.GetProperty("members").EnumerateArray().Single();
        row.GetProperty("approved").GetInt32().Should().Be(1);
        row.GetProperty("pending").GetInt32().Should().Be(1);
        (await member.GetAsync($"/api/rehearsals/stats?from={DateText(1)}&to={DateText(0)}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- the pages ----------

    [Theory]
    [InlineData("/rehearsals")]
    [InlineData("/rehearsals/{id}")]
    public async Task ThePages_AreTheReactShellForMembers_AndSendVisitorsToSignIn(string template)
    {
        var id = await AddRehearsalAsync(NextDay(past: false));
        var path = template.Replace("{id}", id.ToString());
        var (member, _) = await SignInAsync();

        var visitor = await Anonymous().GetAsync(path);
        visitor.StatusCode.Should().Be(HttpStatusCode.Redirect);
        visitor.Headers.Location!.ToString().Should().Be("/login?returnUrl=" + Uri.EscapeDataString(path));

        var page = await member.GetAsync(path);
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        (await page.Content.ReadAsStringAsync()).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");
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
