// RehearsalsApiTests: NoticesAndStats tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;

namespace RTUB.Integration.Tests.Api;

public partial class RehearsalsApiTests
{
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
}
