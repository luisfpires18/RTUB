// PendingActionsApiTests tests: the page, the activity panel and answers.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Integration.Tests.Api;
public partial class PendingActionsApiTests
{
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
        page.GetProperty("panels").EnumerateArray().Select(p => p.GetProperty("key").GetString()).Should().Equal("events", "rehearsals", "logistics", "gallery");
        raw.Should().NotContain(me.Id).And.NotContain("@test.com");
        var keys = await AllKeysAsync(member, "events", "upcoming");
        keys.Should().Contain(mine.ToString()).And.NotContain(answered.ToString());
        (await AllKeysAsync(member, "rehearsals", "upcoming")).Should().Contain(rehearsal.ToString());
        var panel = JsonDocument.Parse(raw).RootElement;
        panel.GetProperty("pageSize").GetInt32().Should().Be(3);
        panel.GetProperty("rows").GetArrayLength().Should().BeLessThanOrEqualTo(3);
    }

    /// <summary>Task 087 (owner decision): a Reformado has no pending action at all, whatever roles or positions they hold.</summary>
    [Fact]
    public async Task AReformado_GetsThePage_WithNoPanelAtAll()
    {
        var (member, me) = await SignInAsync([MemberCategory.Tuno], retired: true, role: "Admin", positions: [Position.Magister]);
        await AreaHttp.WithTokenAsync(member);
        var board = await SeedBoardAsync(withDone: true);
        var card = await AddCardAsync(board.Todo, "Trabalho meu", owner: me.Id);

        (await member.GetAsync("/api/pending-actions")).StatusCode.Should().Be(HttpStatusCode.NotFound, "the page is not for a Reformado at all");
        (await member.GetFromJsonAsync<JsonElement>("/api/account/me")).GetProperty("retired").GetBoolean().Should().BeTrue("the menu hides the entry");
        foreach (var panel in new[] { "events", "logistics", "questions", "news", "members", "treasury", "meetings" })
        {
            (await member.GetAsync($"/api/pending-actions/panels/{panel}")).StatusCode.Should().Be(HttpStatusCode.NotFound, panel);
        }

        (await Answer(member, "logistics.card", card, "done")).StatusCode.Should().Be(HttpStatusCode.NotFound, "no command either");
    }

    [Fact]
    public async Task DismissingAnAnswer_IsRefused_AndNeedsTheAntiforgeryToken()
    {
        var (member, me) = await SignInAsync([MemberCategory.Tuno], retired: false);
        var id = await AddEventAsync(NextDay());
        var body = new { kind = "event.response", sourceKey = id.ToString() };

        (await member.PostAsJsonAsync("/api/pending-actions/dismissals", body)).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "no X-CSRF-TOKEN");
        await AreaHttp.WithTokenAsync(member);
        (await member.PostAsJsonAsync("/api/pending-actions/dismissals", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "an answer is given, not dismissed, whatever the request claims");
        (await member.PostAsJsonAsync("/api/pending-actions/dismissals", new { kind = "event.response", sourceKey = "999999" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PendingActionDismissals.CountAsync(d => d.UserId == me.Id)).Should().Be(0);
    }

    /// <summary>Task 085: the 071 landing on /pending-actions is gone; every member lands on /events unless a deep link asked for a page.</summary>
    [Fact]
    public async Task Login_LandsEveryMemberOnEvents_UnlessTheyAskedForAPage()
    {
        var (active, user) = await NewUserAsync([MemberCategory.Leitao], retired: false);
        var (retired, other) = await NewUserAsync([MemberCategory.Tuno], retired: true);

        (await LoginAsync(active, user, returnUrl: null)).Should().Be("/events");
        (await LoginAsync(Client(), user, returnUrl: "/logistics/3?card=7")).Should().Be("/logistics/3?card=7", "a deep link wins");
        (await LoginAsync(Client(), user, returnUrl: "/pending-actions")).Should().Be("/pending-actions", "asked for explicitly");
        (await LoginAsync(Client(), user, returnUrl: "https://evil.example/")).Should().Be("/events");
        (await LoginAsync(retired, other, returnUrl: null)).Should().Be("/events");

        var again = await active.GetAsync("/login");
        again.Headers.Location!.ToString().Should().Be("/events", "a signed-in member opening /login goes to the same place");
    }

    // ---------- answers (072) ----------

    [Fact]
    public async Task Answers_WriteTheModulesOwnRows_AndTheModulePagesAgree()
    {
        // Past prompts need a past day of this fiscal year: none exists on 1-3 September.
        var today = PortugalTime.Today(TimeProvider.System);
        Assert.SkipWhen((today - new DateTime(today.Month >= 9 ? today.Year : today.Year - 1, 9, 1)).Days < 3,
            "the first days of a fiscal year have no past day to answer yet");
        var (member, me) = await SignInAsync([MemberCategory.Leitao], retired: false);
        await AreaHttp.WithTokenAsync(member);
        var upcoming = await AddEventAsync(NextDay());
        var pastEvent = await AddEventAsync(-1);
        var rehearsal = await AddRehearsalAsync(NextDay());
        var pastRehearsal = await AddRehearsalAsync(-2);
        var missed = await AddRehearsalAsync(-3);

        (await Answer(member, "event.response", upcoming, "yes", "Guitarra", "Levo o estojo")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Answer(member, "event.response", pastEvent, "no")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Answer(member, "rehearsal.response", rehearsal, "no")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Answer(member, "rehearsal.response", pastRehearsal, "yes")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var going = await db.Enrollments.AsNoTracking().SingleAsync(x => x.EventId == upcoming && x.UserId == me.Id);
            going.WillAttend.Should().BeTrue();
            going.Instrument.Should().Be(InstrumentType.Guitarra);
            going.Notes.Should().Be("Levo o estojo");
            going.CategoryAtEvent.Should().Be(MemberCategory.Leitao, "the service's category snapshot");
            (await db.Enrollments.AsNoTracking().SingleAsync(x => x.EventId == pastEvent && x.UserId == me.Id)).WillAttend.Should().BeFalse(
                "a Não fui is kept as an answer, not just a hidden row");
            (await db.RehearsalAttendances.AsNoTracking().SingleAsync(a => a.RehearsalId == rehearsal && a.UserId == me.Id)).WillAttend.Should().BeFalse();
            var selfReport = await db.RehearsalAttendances.AsNoTracking().SingleAsync(a => a.RehearsalId == pastRehearsal && a.UserId == me.Id);
            (selfReport.WillAttend, selfReport.Attended).Should().Be((true, false), "a Fui waits for approval");
            (await db.Rehearsals.CountAsync(r => r.Id == pastRehearsal)).Should().Be(1, "no rehearsal is created");
        }

        // The module pages show the same answers; the page no longer lists them, the unanswered one still is.
        (await member.GetFromJsonAsync<JsonElement>($"/api/events/{upcoming}/enrollment")).GetProperty("status").GetString().Should().Be("going");
        (await member.GetFromJsonAsync<JsonElement>($"/api/rehearsals/{pastRehearsal}")).GetProperty("rehearsal").GetProperty("mine")
            .GetProperty("status").GetString().Should().Be("pending");
        (await AllKeysAsync(member, "events", "upcoming")).Should().NotContain(upcoming.ToString());
        (await AllKeysAsync(member, "events", "past")).Should().NotContain(pastEvent.ToString());
        (await AllKeysAsync(member, "rehearsals", "past")).Should().Contain(missed.ToString()).And.NotContain(pastRehearsal.ToString());
    }

    [Fact]
    public async Task Answers_NeedTheToken_AndRetriesOrRacesNeverWriteTwice()
    {
        var (member, me) = await SignInAsync([MemberCategory.Tuno], retired: false);
        var id = await AddEventAsync(NextDay());

        (await Answer(member, "event.response", id, "yes")).StatusCode.Should().Be(HttpStatusCode.BadRequest, "no X-CSRF-TOKEN");
        await AreaHttp.WithTokenAsync(member);
        var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Answer(member, "event.response", id, i % 2 == 0 ? "yes" : "no")));

        answers.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        answers.Where(r => r.StatusCode != HttpStatusCode.NoContent).Select(r => r.StatusCode).Should()
            .OnlyContain(c => c == HttpStatusCode.NotFound || c == HttpStatusCode.Conflict);
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Enrollments.CountAsync(x => x.EventId == id && x.UserId == me.Id)).Should().Be(1);
    }

    [Fact]
    public async Task AnActivityChangedSinceTheRead_IsRefusedHonestly()
    {
        var (member, me) = await SignInAsync([MemberCategory.Tuno], retired: false);
        await AreaHttp.WithTokenAsync(member);
        var cancelled = await AddEventAsync(NextDay());
        var answeredElsewhere = await AddRehearsalAsync(NextDay());
        await CancelEventAsync(cancelled);
        await AttendAsync(answeredElsewhere, me.Id, approved: true);

        (await Answer(member, "event.response", cancelled, "yes")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Answer(member, "rehearsal.response", answeredElsewhere, "no")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Enrollments.AnyAsync(x => x.EventId == cancelled)).Should().BeFalse();
        (await db.RehearsalAttendances.AsNoTracking().SingleAsync(a => a.RehearsalId == answeredElsewhere && a.UserId == me.Id))
            .Should().Match<RehearsalAttendance>(a => a.WillAttend && a.Attended, "a confirmed presença is never overwritten");
    }

    private async Task AttendAsync(int rehearsalId, string userId, bool approved)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var a = RehearsalAttendance.Create(rehearsalId, userId);
        a.MarkAttendance(approved);
        db.RehearsalAttendances.Add(a);
        await db.SaveChangesAsync();
    }

    private async Task AnswerEventAsync(int eventId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Enrollments.Add(Enrollment.Create(userId, eventId));
        await db.SaveChangesAsync();
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

    private HttpClient Anonymous() => Client();
}
