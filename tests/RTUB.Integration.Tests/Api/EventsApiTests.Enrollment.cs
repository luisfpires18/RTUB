// EventsApiTests tests: member answers, withdrawals and video plays.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;

namespace RTUB.Integration.Tests.Api;
public partial class EventsApiTests
{
    // ---------- enrollment ----------

    [Fact]
    public async Task Enrollment_IsForSignedInMembersOnly()
    {
        var seed = await SeedAsync();
        var visitor = Anonymous();
        await AreaHttp.WithTokenAsync(visitor);

        (await visitor.GetAsync($"/api/events/{seed.Open}/enrollment")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await visitor.PutAsJsonAsync($"/api/events/{seed.Open}/enrollment", Answer(true))).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await visitor.DeleteAsync($"/api/events/{seed.Past}/enrollment")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Writes_WithoutTheAntiforgeryHeader_AreRefused_AndChangeNothing()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync();

        var put = await member.PutAsJsonAsync($"/api/events/{seed.Open}/enrollment", Answer(true));
        var delete = await member.DeleteAsync($"/api/events/{seed.Past}/enrollment");
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
        await AreaHttp.WithTokenAsync(member);

        var going = await member.PutAsJsonAsync($"/api/events/{seed.Open}/enrollment", Answer(true, notes: "levo capa"));
        going.StatusCode.Should().Be(HttpStatusCode.OK);
        (await going.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("going");

        var notGoing = await member.PutAsJsonAsync($"/api/events/{seed.Open}/enrollment", Answer(false, notes: "doente"));
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
        await AreaHttp.WithTokenAsync(member);

        var response = await member.PutAsJsonAsync($"/api/events/{seed.Open}/enrollment", Answer(true, instrument: "Violino"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("instrument", out _).Should().BeTrue();
        (await EnrollmentAsync(seed.Open, user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task CancelledAndPastEvents_TakeNoNewAnswer()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);

        (await member.PutAsJsonAsync($"/api/events/{seed.Cancelled}/enrollment", Answer(true))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await member.PutAsJsonAsync($"/api/events/{seed.Past}/enrollment", Answer(true))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await EnrollmentAsync(seed.Cancelled, user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task APastEvent_LetsTheMemberWhoWentWithdraw()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync();
        await AddEnrollmentAsync(seed.Past, user.Id, willAttend: true);
        await AreaHttp.WithTokenAsync(member);

        var response = await member.DeleteAsync($"/api/events/{seed.Past}/enrollment");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnrollmentAsync(seed.Past, user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AVideoPlay_IsRecorded_ForVisitorsToo()
    {
        var seed = await SeedAsync();
        var visitor = Anonymous();
        await AreaHttp.WithTokenAsync(visitor);

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

    private async Task<int> CountEnrollmentsAsync(int eventId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Enrollments
            .CountAsync(e => e.EventId == eventId && e.UserId == userId);
    }
}
